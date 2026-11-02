using System.Text.Json;
using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Sessions;

/// <summary>
/// Target-tier-with-top-up decision (spec 6.4). At the tier decision time the confirmed count is fixed:
/// if it reaches the target tier minimum the session is confirmed; otherwise each confirmed student owes the
/// difference to the price of the tier that matches the actual count, and is asked to choose.
/// The participant count used for pricing never goes below the count at decision time, so a student who opts out
/// never makes the others pay more (Dose absorbs that). Late payment approvals can only lower what others owe.
/// All steps are idempotent and safe to re-run.
/// </summary>
public sealed class GroupDecisionService(IDoseDbContext db, IClock clock, SettingsService settings, Notifications.NotificationService notify)
{
    public const string SystemActor = "system";

    /// <summary>Runs every due decision. Called by the background job; returns the number of sessions decided.</summary>
    public async Task<int> RunDueDecisionsAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.Sessions.Where(s => s.Mode == SessionMode.Group && !s.IsClosed && s.Status == SessionStatus.Published
                                               && s.TierDecidedAtUtc == null && s.TierDecisionAtUtc <= now)
            .Select(s => s.Id).ToListAsync(ct);
        foreach (var id in due) await DecideAsync(id, SystemActor, ct);
        return due.Count;
    }

    public async Task DecideAsync(Guid sessionId, string actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);
        var session = await db.Sessions.Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Tiers)
            .Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Plan)
            .Include(s => s.TargetTier).AsSplitQuery()
            .SingleAsync(s => s.Id == sessionId, ct);
        if (session.TierDecidedAtUtc is not null || session.Mode != SessionMode.Group || session.IsClosed || session.Status != SessionStatus.Published) return;

        var now = clock.UtcNow;
        var confirmed = await db.Bookings.CountAsync(b => b.SessionId == sessionId && b.Status == BookingStatus.Confirmed, ct);
        session.TierDecidedAtUtc = now;
        session.DecisionConfirmedCount = confirmed;

        await RecalculateAsync(session, actor, ct);
        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor, Entity = nameof(Session), EntityId = sessionId.ToString(), Action = "TierDecision",
            DataJson = JsonSerializer.Serialize(new { confirmed, min = session.MinParticipants, status = session.Status.ToString() }),
            AtUtc = now,
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Recomputes what each participant owes after the decision. Must run inside a transaction holding the session lock.
    /// Pending top-ups whose amount changed are cancelled and re-issued (ledger amounts are never edited).
    /// </summary>
    public async Task RecalculateAsync(Session session, string actor, CancellationToken ct)
    {
        if (session.TierDecidedAtUtc is null || session.Mode != SessionMode.Group) return;
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var now = clock.UtcNow;
        var version = session.PricingPlanVersion
                      ?? await db.PricingPlanVersions.Include(v => v.Tiers).SingleAsync(v => v.Id == session.PricingPlanVersionId, ct);

        var participants = await db.Bookings
            .Where(b => b.SessionId == session.Id && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.TopUpPending))
            .ToListAsync(ct);
        var n = Math.Max(session.DecisionConfirmedCount ?? 0, participants.Count);

        var ids = participants.Select(b => b.Id).ToList();
        var pending = await db.FinancialAdjustments
            .Where(a => ids.Contains(a.BookingId) && a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Pending)
            .ToListAsync(ct);
        var settled = await db.FinancialAdjustments
            .Where(a => ids.Contains(a.BookingId) && a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Settled)
            .GroupBy(a => a.BookingId).Select(g => new { g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var waived = await db.FinancialAdjustments
            .Where(a => ids.Contains(a.BookingId) && a.Type == AdjustmentType.Waiver)
            .Select(a => a.BookingId).Distinct().ToListAsync(ct);

        foreach (var b in participants)
        {
            var paid = b.QuotedPrice + settled.GetValueOrDefault(b.Id);
            // The student's promo stays: a percentage also comes off the new price, a fixed amount was already taken once.
            var due = waived.Contains(b.Id) ? 0 : Math.Max(0, b.NetPrice(GroupPricing.PriceForCount(version, Math.Max(1, n))) - paid);
            var current = pending.FirstOrDefault(a => a.BookingId == b.Id);

            if (current is not null && current.Amount == due) continue;
            if (current is not null)
            {
                current.Status = AdjustmentStatus.Cancelled;
                current.ResolvedBy = actor;
                current.ResolvedAtUtc = now;
                current.ResolutionNote = due == 0 ? "لم يعد مطلوباً بعد اكتمال العدد" : "أعيد الحساب";
            }
            if (due > 0)
            {
                var adj = new FinancialAdjustment
                {
                    BookingId = b.Id, Type = AdjustmentType.TopUpDue, Amount = due, Currency = b.Currency,
                    RespondByUtc = current?.RespondByUtc ?? now.AddHours(cfg.TopUpResponseHours),
                    Reason = $"الجروب اتحسم على {n} طلاب بدل {session.MinParticipants} على الأقل",
                    CreatedBy = actor, CreatedAtUtc = now,
                    StudentChoice = current?.StudentChoice, ChoiceAtUtc = current?.ChoiceAtUtc,
                };
                db.FinancialAdjustments.Add(adj);
                await notify.QueueForBookingAsync(b.Id, Domain.Notifications.TemplateCodes.TopUpRequired, $"topup:{adj.Id}", ct,
                    new Dictionary<string, string> { ["amount"] = due.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                b.Status = BookingStatus.TopUpPending;
            }
            else
            {
                b.Status = BookingStatus.Confirmed;
            }
        }
        await db.SaveChangesAsync(ct);
        await RefreshStatusAsync(session, ct);
    }

    /// <summary>Decided group session: TopUpRequired while anyone still owes or must choose, otherwise Confirmed.</summary>
    public async Task RefreshStatusAsync(Session session, CancellationToken ct)
    {
        if (session.TierDecidedAtUtc is null || session.Status is SessionStatus.Cancelled or SessionStatus.Completed or SessionStatus.InProgress) return;
        var anyPending = await db.Bookings.AnyAsync(b => b.SessionId == session.Id && b.Status == BookingStatus.TopUpPending, ct);
        var anyConfirmed = await db.Bookings.AnyAsync(b => b.SessionId == session.Id && b.Status == BookingStatus.Confirmed, ct);
        session.Status = anyPending || !anyConfirmed ? SessionStatus.TopUpRequired : SessionStatus.Confirmed;
    }
}
