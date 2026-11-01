using Dose.Application.Abstractions;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Administration;

/// <summary>Admin home: what needs attention now, what's coming up, and basic numbers for the period (spec 9.8).</summary>
public sealed class DashboardService(IDoseDbContext db, IClock clock)
{
    public async Task<AdminDashboardDto> GetAsync(int periodDays, CancellationToken ct)
    {
        periodDays = Math.Clamp(periodDays, 1, 365);
        var now = clock.UtcNow;
        var from = now.AddDays(-periodDays);

        var pending = db.PaymentProofs.Where(p => p.Status == PaymentProofStatus.Submitted);
        var pendingCount = await pending.CountAsync(ct);
        DateTime? oldest = pendingCount == 0 ? null : await pending.MinAsync(p => (DateTime?)p.SubmittedAtUtc, ct);

        var topUps = db.FinancialAdjustments.Where(a => a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Pending);
        var openTopUps = await topUps.CountAsync(ct);
        var overdue = await topUps.CountAsync(a => a.StudentChoice == null && a.RespondByUtc < now, ct);
        var refunds = await db.Bookings.CountAsync(b => b.Status == BookingStatus.RefundPending, ct);
        var transfers = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Transferred && b.FollowUpResolvedAtUtc == null, ct);
        var requests = await db.SessionRequests.CountAsync(r => r.Status == SessionRequestStatus.New, ct);
        var availability = await db.TeacherAvailabilities.CountAsync(a => a.Status == AvailabilityStatus.Submitted && a.EndUtc > now, ct);

        var horizon = now.AddHours(48);
        var upcoming = await Sessions(s => s.StartUtc >= now.AddHours(-2) && s.StartUtc <= horizon
                                           && s.Status != SessionStatus.Cancelled && s.Status != SessionStatus.Draft
                                           && s.Status != SessionStatus.Completed, ct);
        var nearDecision = await Sessions(s => s.Mode == SessionMode.Group && s.Status == SessionStatus.Published
                                               && s.TierDecidedAtUtc == null && s.TierDecisionAtUtc <= horizon, ct);

        var confirmedInPeriod = await db.Bookings.CountAsync(b => b.ConfirmedAtUtc >= from && b.PriceSnapshotId != null, ct);
        var collectedMain = await db.Bookings.Where(b => b.ConfirmedAtUtc >= from && b.PriceSnapshotId != null)
            .SumAsync(b => (decimal?)b.PriceSnapshot!.FinalAmount, ct) ?? 0;
        var collectedTopUps = await db.FinancialAdjustments
            .Where(a => a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Settled && a.ResolvedAtUtc >= from)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0;
        var refunded = await db.FinancialAdjustments
            .Where(a => a.Type == AdjustmentType.Refund && a.Status == AdjustmentStatus.Completed && a.ResolvedAtUtc >= from)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0;

        return new AdminDashboardDto(pendingCount, oldest is null ? null : Math.Round((now - oldest.Value).TotalMinutes),
            openTopUps, overdue, refunds, transfers, requests, availability, upcoming, nearDecision,
            periodDays, confirmedInPeriod, collectedMain + collectedTopUps, refunded, "EGP",
            await db.Notifications.CountAsync(n => n.Status == Domain.Notifications.NotificationStatus.Pending && n.DueAtUtc <= now.AddHours(1), ct));
    }

    private async Task<List<DashboardSessionDto>> Sessions(System.Linq.Expressions.Expression<Func<Session, bool>> filter, CancellationToken ct)
    {
        var rows = await db.Sessions.AsNoTracking().Where(filter).OrderBy(s => s.StartUtc).Take(20)
            .Select(s => new
            {
                s.Id, s.ScopeTitleAr, Subject = s.Subject!.NameAr, Teacher = s.Teacher!.DisplayNameAr, s.StartUtc, s.Mode, s.Status,
                Confirmed = db.Bookings.Count(b => b.SessionId == s.Id && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.TopUpPending)),
                s.Capacity, s.MinParticipants, HasUrl = s.MeetingUrl != null, s.TierDecisionAtUtc,
            }).ToListAsync(ct);
        return rows.Select(r => new DashboardSessionDto(r.Id, r.ScopeTitleAr, r.Subject, r.Teacher, r.StartUtc, r.Mode.ToString(),
            r.Status.ToString(), r.Confirmed, r.Capacity, r.MinParticipants, r.HasUrl, r.TierDecisionAtUtc)).ToList();
    }
}
