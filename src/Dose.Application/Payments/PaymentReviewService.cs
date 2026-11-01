using System.Text.Json;
using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Contracts;
using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Payments;

/// <summary>Admin review of payment proofs. A booking is confirmed only here (spec 8.1).</summary>
public sealed class PaymentReviewService(IDoseDbContext db, IClock clock, SettingsService settings, IFileStorage files, Sessions.GroupDecisionService decisions)
{
    public async Task<IReadOnlyList<PaymentProofAdminDto>> ListAsync(PaymentProofStatus? status, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var q = db.PaymentProofs.AsNoTracking();
        if (status is { } s) q = q.Where(p => p.Status == s);
        var rows = await q.OrderBy(p => p.SubmittedAtUtc).Take(200)
            .Select(p => new
            {
                p.Id, p.Booking!.Reference, BookingStatus = p.Booking.Status,
                StudentName = p.Booking.Student!.Name, p.Booking.Student.ContactPhoneE164, p.Booking.Student.GuardianPhoneE164,
                p.Booking.Session!.ScopeTitleAr, p.Booking.Session.StartUtc,
                MethodNameAr = p.PaymentMethod!.NameAr, p.Amount, p.Currency, p.SenderReference, p.Channel, p.Status, p.SubmittedAtUtc, IsTopUp = p.AdjustmentId != null,
            })
            .ToListAsync(ct);
        return rows.Select(r => new PaymentProofAdminDto(
            r.Id, r.Reference, r.BookingStatus.ToString(), r.StudentName, r.ContactPhoneE164, r.GuardianPhoneE164,
            r.ScopeTitleAr, r.StartUtc, r.MethodNameAr, r.Amount, r.Currency, r.SenderReference, r.Channel.ToString(),
            r.Status.ToString(), r.SubmittedAtUtc, Math.Round((now - r.SubmittedAtUtc).TotalMinutes), r.IsTopUp)).ToList();
    }

    public async Task<(Stream Content, string ContentType)> OpenFileAsync(Guid proofId, CancellationToken ct)
    {
        var proof = await db.PaymentProofs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proofId, ct)
                    ?? throw new DomainException("payment.proof_not_found", "إيصال الدفع مش موجود.", "Payment proof not found.");
        var stream = await files.OpenAsync(proof.FileKey, ct)
                     ?? throw new DomainException("payment.file_missing", "صورة الإيصال مش موجودة في التخزين.", "The screenshot file is missing from storage.");
        return (stream, proof.FileContentType);
    }

    /// <summary>Idempotent: approving an already-approved proof returns without changes.</summary>
    public async Task ApproveAsync(Guid proofId, string actor, CancellationToken ct)
    {
        var sessionId = await db.PaymentProofs.Where(p => p.Id == proofId).Select(p => (Guid?)p.Booking!.SessionId).FirstOrDefaultAsync(ct)
                        ?? throw new DomainException("payment.proof_not_found", "إيصال الدفع مش موجود.", "Payment proof not found.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);

        var proof = await db.PaymentProofs.Include(p => p.Booking).SingleAsync(p => p.Id == proofId, ct);
        if (proof.Status == PaymentProofStatus.Approved) return;
        if (proof.Status == PaymentProofStatus.Rejected)
            throw new DomainException(BookingErrors.InvalidState, "الإيصال ده اترفض قبل كده.", "This proof was already rejected.");
        var booking = proof.Booking!;
        if (proof.AdjustmentId is { } adjustmentId)
        {
            await ApproveTopUpAsync(proof, adjustmentId, sessionId, actor, ct);
            await tx.CommitAsync(ct);
            return;
        }
        if (booking.Status != BookingStatus.PaymentUnderReview)
            throw new DomainException(BookingErrors.InvalidState, "الحجز ده مش تحت مراجعة الدفع.", $"Booking is {booking.Status}, not under payment review.");

        var session = await db.Sessions.AsNoTracking()
            .Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Plan)
            .Include(s => s.TargetTier)
            .SingleAsync(s => s.Id == sessionId, ct);
        var version = session.PricingPlanVersion!;
        var now = clock.UtcNow;

        var snapshot = new PriceSnapshot
        {
            PlanId = version.PlanId,
            PlanVersionId = version.Id,
            Currency = booking.Currency,
            BasePrice = booking.QuotedPrice,
            TargetTierId = session.TargetTierId,
            TierMin = session.TargetTier?.MinParticipants,
            TierMax = session.TargetTier?.MaxParticipants,
            WorstCasePrice = booking.QuotedWorstCasePrice,
            FinalAmount = booking.QuotedPrice,
            Policy = session.Policy,
            CalculatedAtUtc = now,
        };
        db.PriceSnapshots.Add(snapshot);

        proof.Status = PaymentProofStatus.Approved;
        proof.ReviewedBy = actor;
        proof.ReviewedAtUtc = now;
        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAtUtc = now;
        booking.PriceSnapshotId = snapshot.Id;

        // The version is now referenced by a confirmed price: it must never change.
        await db.PricingPlanVersions.Where(v => v.Id == version.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(v => v.IsLocked, true), ct);

        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor, Entity = nameof(Booking), EntityId = booking.Id.ToString(), Action = "PaymentApproved",
            DataJson = JsonSerializer.Serialize(new { proofId, booking.Reference, amount = booking.QuotedPrice }), AtUtc = now,
        });
        await db.SaveChangesAsync(ct);

        // Payment approved after the group was decided: it may lower what the others owe (spec 6.4 rule 7).
        var tracked = await db.Sessions.SingleAsync(s => s.Id == sessionId, ct);
        if (tracked.TierDecidedAtUtc is not null)
        {
            await decisions.RecalculateAsync(tracked, actor, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private async Task ApproveTopUpAsync(PaymentProof proof, Guid adjustmentId, Guid sessionId, string actor, CancellationToken ct)
    {
        var adj = await db.FinancialAdjustments.SingleAsync(a => a.Id == adjustmentId, ct);
        if (adj.Status != AdjustmentStatus.Pending)
            throw new DomainException(BookingErrors.InvalidState, "الفرق ده مبقاش مطلوب (اتلغى أو اتعاد حسابه). ارفض الإيصال وراجع مع الطالب.", "This top-up is no longer due (cancelled or recalculated). Reject the proof and check with the student.");
        if (proof.Amount < adj.Amount)
            throw new DomainException(BookingErrors.InvalidState, "مبلغ الإيصال أقل من الفرق المطلوب.", "The proof amount is less than the top-up due.");
        var now = clock.UtcNow;
        proof.Status = PaymentProofStatus.Approved;
        proof.ReviewedBy = actor;
        proof.ReviewedAtUtc = now;
        adj.Status = AdjustmentStatus.Settled;
        adj.ResolvedBy = actor;
        adj.ResolvedAtUtc = now;
        proof.Booking!.Status = BookingStatus.Confirmed;
        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor, Entity = nameof(Booking), EntityId = proof.BookingId.ToString(), Action = "TopUpApproved",
            DataJson = JsonSerializer.Serialize(new { proofId = proof.Id, adj.Amount }), AtUtc = now,
        });
        await db.SaveChangesAsync(ct);
        var session = await db.Sessions.SingleAsync(s => s.Id == sessionId, ct);
        await decisions.RefreshStatusAsync(session, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(Guid proofId, string reason, string actor, CancellationToken ct)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length is < 3 or > 300)
            throw new DomainException("payment.reason_required", "اكتب سبب قصير للطالب (من 3 لـ 300 حرف).", "Please write a short reason for the student (3–300 characters).");
        var cfg = await settings.GetBookingSettingsAsync(ct);

        var proof = await db.PaymentProofs.Include(p => p.Booking).FirstOrDefaultAsync(p => p.Id == proofId, ct)
                    ?? throw new DomainException("payment.proof_not_found", "إيصال الدفع مش موجود.", "Payment proof not found.");
        if (proof.Status == PaymentProofStatus.Rejected) return;
        if (proof.Status == PaymentProofStatus.Approved)
            throw new DomainException(BookingErrors.InvalidState, "الإيصال ده اتأكد قبل كده.", "This proof was already approved.");

        var now = clock.UtcNow;
        proof.Status = PaymentProofStatus.Rejected;
        proof.RejectionReason = reason;
        proof.ReviewedBy = actor;
        proof.ReviewedAtUtc = now;
        if (proof.AdjustmentId is null && proof.Booking!.Status == BookingStatus.PaymentUnderReview)
        {
            proof.Booking!.Status = BookingStatus.PendingPayment;
            proof.Booking.PaymentDueAtUtc = now.AddMinutes(cfg.ProofResubmitMinutes);
        }
        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor, Entity = nameof(Booking), EntityId = proof.BookingId.ToString(), Action = "PaymentRejected",
            DataJson = JsonSerializer.Serialize(new { proofId, reason }), AtUtc = now,
        });
        await db.SaveChangesAsync(ct);
    }
}
