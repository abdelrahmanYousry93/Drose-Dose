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
public sealed class PaymentReviewService(IDoseDbContext db, IClock clock, SettingsService settings, IFileStorage files)
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
                MethodNameAr = p.PaymentMethod!.NameAr, p.Amount, p.Currency, p.SenderReference, p.Channel, p.Status, p.SubmittedAtUtc,
            })
            .ToListAsync(ct);
        return rows.Select(r => new PaymentProofAdminDto(
            r.Id, r.Reference, r.BookingStatus.ToString(), r.StudentName, r.ContactPhoneE164, r.GuardianPhoneE164,
            r.ScopeTitleAr, r.StartUtc, r.MethodNameAr, r.Amount, r.Currency, r.SenderReference, r.Channel.ToString(),
            r.Status.ToString(), r.SubmittedAtUtc, Math.Round((now - r.SubmittedAtUtc).TotalMinutes))).ToList();
    }

    public async Task<(Stream Content, string ContentType)> OpenFileAsync(Guid proofId, CancellationToken ct)
    {
        var proof = await db.PaymentProofs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proofId, ct)
                    ?? throw new DomainException("payment.proof_not_found", "Payment proof not found.");
        var stream = await files.OpenAsync(proof.FileKey, ct)
                     ?? throw new DomainException("payment.file_missing", "The screenshot file is missing from storage.");
        return (stream, proof.FileContentType);
    }

    /// <summary>Idempotent: approving an already-approved proof returns without changes.</summary>
    public async Task ApproveAsync(Guid proofId, string actor, CancellationToken ct)
    {
        var sessionId = await db.PaymentProofs.Where(p => p.Id == proofId).Select(p => (Guid?)p.Booking!.SessionId).FirstOrDefaultAsync(ct)
                        ?? throw new DomainException("payment.proof_not_found", "Payment proof not found.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);

        var proof = await db.PaymentProofs.Include(p => p.Booking).SingleAsync(p => p.Id == proofId, ct);
        if (proof.Status == PaymentProofStatus.Approved) return;
        if (proof.Status == PaymentProofStatus.Rejected)
            throw new DomainException(BookingErrors.InvalidState, "This proof was already rejected.");
        var booking = proof.Booking!;
        if (booking.Status != BookingStatus.PaymentUnderReview)
            throw new DomainException(BookingErrors.InvalidState, $"Booking is {booking.Status}, not under payment review.");

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
        await tx.CommitAsync(ct);
    }

    public async Task RejectAsync(Guid proofId, string reason, string actor, CancellationToken ct)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length is < 3 or > 300)
            throw new DomainException("payment.reason_required", "Please write a short reason for the student (3–300 characters).");
        var cfg = await settings.GetBookingSettingsAsync(ct);

        var proof = await db.PaymentProofs.Include(p => p.Booking).FirstOrDefaultAsync(p => p.Id == proofId, ct)
                    ?? throw new DomainException("payment.proof_not_found", "Payment proof not found.");
        if (proof.Status == PaymentProofStatus.Rejected) return;
        if (proof.Status == PaymentProofStatus.Approved)
            throw new DomainException(BookingErrors.InvalidState, "This proof was already approved.");

        var now = clock.UtcNow;
        proof.Status = PaymentProofStatus.Rejected;
        proof.RejectionReason = reason;
        proof.ReviewedBy = actor;
        proof.ReviewedAtUtc = now;
        if (proof.Booking!.Status == BookingStatus.PaymentUnderReview)
        {
            proof.Booking.Status = BookingStatus.PendingPayment;
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
