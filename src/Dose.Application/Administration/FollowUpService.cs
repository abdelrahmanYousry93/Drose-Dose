using Dose.Application.Abstractions;
using Dose.Application.Sessions;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Administration;

/// <summary>
/// Admin follow-ups after a group decision or a cancellation: students who owe a top-up, refunds to pay back,
/// and transfers to arrange. Money moves outside the platform (InstaPay / Vodafone Cash); the platform records it.
/// </summary>
public sealed class FollowUpService(IDoseDbContext db, IClock clock, GroupDecisionService decisions, Notifications.NotificationService notify)
{
    public async Task<IReadOnlyList<FollowUpDto>> ListAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.TopUpPending
                        || (b.Status == BookingStatus.RefundPending)
                        || (b.Status == BookingStatus.Transferred && b.FollowUpResolvedAtUtc == null))
            .Select(b => new
            {
                b.Id, b.Reference, b.Status, b.QuotedPrice, b.Currency, b.CancelReason, b.SessionId,
                StudentName = (string?)b.Student!.Name, Phone = (string?)b.Student.ContactPhoneE164, Guardian = b.Student.GuardianPhoneE164,
                b.Session!.ScopeTitleAr, b.Session.StartUtc,
            })
            .OrderBy(b => b.StartUtc).Take(300).ToListAsync(ct);

        var ids = bookings.Select(b => b.Id).ToList();
        var adjustments = await db.FinancialAdjustments.AsNoTracking()
            .Where(a => ids.Contains(a.BookingId) && a.Type == AdjustmentType.TopUpDue).ToListAsync(ct);
        var pendingIds = adjustments.Where(a => a.Status == AdjustmentStatus.Pending).Select(a => a.Id).ToList();
        var proofs = await db.PaymentProofs.AsNoTracking().Where(p => p.AdjustmentId != null && pendingIds.Contains(p.AdjustmentId.Value))
            .GroupBy(p => p.AdjustmentId).Select(g => g.OrderByDescending(p => p.SubmittedAtUtc).First())
            .ToListAsync(ct);

        return bookings.Select(b =>
        {
            var pending = adjustments.FirstOrDefault(a => a.BookingId == b.Id && a.Status == AdjustmentStatus.Pending);
            var settled = adjustments.Where(a => a.BookingId == b.Id && a.Status == AdjustmentStatus.Settled).Sum(a => a.Amount);
            var proof = pending is null ? null : proofs.FirstOrDefault(p => p.AdjustmentId == pending.Id);
            var kind = b.Status switch
            {
                BookingStatus.TopUpPending => "TopUp",
                BookingStatus.RefundPending => "Refund",
                _ => "Transfer",
            };
            return new FollowUpDto(b.Id, kind, b.Reference, b.Status.ToString(), b.StudentName, b.Phone, b.Guardian, b.SessionId,
                b.ScopeTitleAr, b.StartUtc, b.QuotedPrice + settled, b.Currency, pending?.Amount, pending?.RespondByUtc,
                pending?.StudentChoice?.ToString(), proof?.Status.ToString(), pending?.Reason ?? b.CancelReason,
                pending?.RespondByUtc < now && pending.StudentChoice is null);
        }).ToList();
    }

    /// <summary>Dose absorbs the difference: the top-up is cancelled and a waiver is recorded.</summary>
    public async Task WaiveAsync(Guid bookingId, string? note, string actor, CancellationToken ct)
    {
        var (booking, tx) = await LockAsync(bookingId, ct);
        await using var _ = tx;
        if (booking.Status != BookingStatus.TopUpPending) throw new DomainException(AdminErrors.Invalid, "الحجز ده مفيهوش فرق مطلوب.", "This booking has no top-up due.");
        var adj = await db.FinancialAdjustments.SingleAsync(a => a.BookingId == bookingId && a.Type == AdjustmentType.TopUpDue
                                                                && a.Status == AdjustmentStatus.Pending, ct);
        var now = clock.UtcNow;
        adj.Status = AdjustmentStatus.Cancelled;
        adj.ResolvedBy = actor;
        adj.ResolvedAtUtc = now;
        adj.ResolutionNote = "تنازل Dose عن الفرق";
        db.FinancialAdjustments.Add(new FinancialAdjustment
        {
            BookingId = bookingId, Type = AdjustmentType.Waiver, Amount = adj.Amount, Currency = adj.Currency,
            Status = AdjustmentStatus.Completed, Reason = AdminGuard.Optional(note, 500) ?? "تنازل عن الفرق",
            CreatedBy = actor, CreatedAtUtc = now, ResolvedBy = actor, ResolvedAtUtc = now,
        });
        booking.Status = BookingStatus.Confirmed;
        db.Audit(clock, actor, nameof(Booking), bookingId, "WaiveTopUp", new { adj.Amount, note });
        await FinishAsync(booking.SessionId, tx, ct);
    }

    /// <summary>Records that the student's money was sent back (outside the platform).</summary>
    public async Task MarkRefundedAsync(Guid bookingId, string? note, string actor, CancellationToken ct)
    {
        var (booking, tx) = await LockAsync(bookingId, ct);
        await using var _ = tx;
        if (booking.Status != BookingStatus.RefundPending) throw new DomainException(AdminErrors.Invalid, "الحجز ده مش منتظر استرداد.", "This booking is not waiting for a refund.");
        var settled = await db.FinancialAdjustments.Where(a => a.BookingId == bookingId && a.Type == AdjustmentType.TopUpDue
                                                               && a.Status == AdjustmentStatus.Settled).SumAsync(a => a.Amount, ct);
        var paidMain = await db.PaymentProofs.AnyAsync(p => p.BookingId == bookingId && p.AdjustmentId == null
                                                            && p.Status != PaymentProofStatus.Rejected, ct);
        var now = clock.UtcNow;
        var amount = (paidMain ? booking.QuotedPrice : 0) + settled;
        db.FinancialAdjustments.Add(new FinancialAdjustment
        {
            BookingId = bookingId, Type = AdjustmentType.Refund, Amount = amount, Currency = booking.Currency,
            Status = AdjustmentStatus.Completed, Reason = AdminGuard.Optional(note, 500) ?? booking.CancelReason,
            CreatedBy = actor, CreatedAtUtc = now, ResolvedBy = actor, ResolvedAtUtc = now,
        });
        booking.Status = BookingStatus.Refunded;
        booking.FollowUpResolvedAtUtc = now;
        db.Audit(clock, actor, nameof(Booking), bookingId, "Refunded", new { amount, note });
        await notify.QueueForBookingAsync(bookingId, Domain.Notifications.TemplateCodes.RefundCompleted, $"refund:{bookingId}", ct,
            new Dictionary<string, string> { ["amount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        await FinishAsync(booking.SessionId, tx, ct);
    }

    /// <summary>The student was moved to another session (arranged with them on WhatsApp).</summary>
    public async Task MarkTransferDoneAsync(Guid bookingId, string? note, string actor, CancellationToken ct)
    {
        var (booking, tx) = await LockAsync(bookingId, ct);
        await using var _ = tx;
        if (booking.Status != BookingStatus.Transferred) throw new DomainException(AdminErrors.Invalid, "الحجز ده مش منتظر نقل.", "This booking is not waiting for a transfer.");
        booking.FollowUpResolvedAtUtc = clock.UtcNow;
        db.Audit(clock, actor, nameof(Booking), bookingId, "TransferDone", new { note });
        await FinishAsync(booking.SessionId, tx, ct);
    }

    private async Task<(Booking, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction)> LockAsync(Guid bookingId, CancellationToken ct)
    {
        var sessionId = await db.Bookings.Where(b => b.Id == bookingId).Select(b => (Guid?)b.SessionId).FirstOrDefaultAsync(ct)
                        ?? throw new DomainException(AdminErrors.NotFound, "الحجز مش موجود.", "Booking not found.");
        var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);
        return (await db.Bookings.SingleAsync(b => b.Id == bookingId, ct), tx);
    }

    private async Task FinishAsync(Guid sessionId, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var session = await db.Sessions.SingleAsync(s => s.Id == sessionId, ct);
        await decisions.RefreshStatusAsync(session, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
