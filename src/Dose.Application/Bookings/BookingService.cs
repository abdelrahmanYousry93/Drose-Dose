using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Application.Sessions;
using Dose.Contracts;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Bookings;

/// <summary>
/// Student booking workflow (spec 8): hold → details → payment proof. Every seat-affecting step
/// runs in a transaction holding an update lock on the session row, so capacity can't be oversold.
/// </summary>
public sealed class BookingService(IDoseDbContext db, IClock clock, SettingsService settings, IFileStorage files)
{
    public async Task<CreateHoldResponse> CreateHoldAsync(Guid sessionId, CancellationToken ct)
    {
        var cfg = await settings.GetBookingSettingsAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);

        var session = await LoadSessionAsync(sessionId, ct)
                      ?? throw new DomainException(BookingErrors.SessionNotFound, "Session not found.");
        var now = clock.UtcNow;
        if (!session.IsOpenForBooking(now))
            throw new DomainException(BookingErrors.SessionClosed, "This session is no longer open for booking.");

        await ExpireStaleAsync(sessionId, now, ct);
        var occupied = await db.Bookings.Where(b => b.SessionId == sessionId).CountAsync(SeatRules.OccupiesSeat(now), ct);
        if (occupied >= session.Capacity)
            throw new DomainException(BookingErrors.SessionFull, "No seats left in this session.");

        var price = SessionPricing.CurrentPrice(session);
        var terms = SessionPricing.GroupTerms(session);
        var (token, hash) = AccessTokens.Create();
        var booking = new Booking
        {
            Reference = AccessTokens.NewReference(),
            SessionId = sessionId,
            Status = BookingStatus.PendingHold,
            CreatedAtUtc = now,
            HoldExpiresAtUtc = now.AddMinutes(cfg.HoldMinutes),
            QuotedPrice = price,
            QuotedWorstCasePrice = terms?.WorstCasePrice,
            Currency = session.PricingPlanVersion!.Plan!.Currency,
            AccessTokenHash = hash,
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CreateHoldResponse(booking.Id, booking.Reference, token, booking.HoldExpiresAtUtc!.Value,
            price, terms?.WorstCasePrice, booking.Currency);
    }

    public async Task SubmitDetailsAsync(string token, SubmitDetailsRequest req, CancellationToken ct)
    {
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var bookingId = await FindIdAsync(token, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var sessionId = await db.Bookings.Where(b => b.Id == bookingId).Select(b => b.SessionId).SingleAsync(ct);
        await db.LockSessionAsync(sessionId, ct);

        var now = clock.UtcNow;
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
        if (booking.Status != BookingStatus.PendingHold)
            throw new DomainException(BookingErrors.InvalidState, "Details were already submitted for this booking.");
        if (booking.IsExpired(now))
        {
            booking.Status = BookingStatus.Expired;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new DomainException(BookingErrors.Expired, "The seat hold expired. Please start the booking again.");
        }

        var session = await LoadSessionAsync(sessionId, ct) ?? throw new DomainException(BookingErrors.SessionNotFound, "Session not found.");
        if (!session.IsOpenForBooking(now))
            throw new DomainException(BookingErrors.SessionClosed, "This session is no longer open for booking.");

        if (!req.AcceptTerms)
            throw new DomainException(BookingErrors.TermsRequired, "You must accept the booking terms.");
        if (session.Mode == SessionMode.Group && !req.AcceptTopUpTerms)
            throw new DomainException(BookingErrors.TermsRequired, "You must accept the group terms (possible difference if the group does not fill).");

        var name = (req.StudentName ?? "").Trim();
        if (name.Length is < 2 or > 100)
            throw new DomainException("booking.invalid_name", "Student name must be between 2 and 100 characters.");
        var phone = PhoneNumber.TryNormalize(req.ContactPhone)
                    ?? throw new DomainException(BookingErrors.InvalidPhone, "WhatsApp number is not valid.");

        string? guardianName = string.IsNullOrWhiteSpace(req.GuardianName) ? null : req.GuardianName.Trim();
        string? guardianPhone = null;
        if (!string.IsNullOrWhiteSpace(req.GuardianPhone))
            guardianPhone = PhoneNumber.TryNormalize(req.GuardianPhone)
                            ?? throw new DomainException(BookingErrors.InvalidPhone, "Guardian WhatsApp number is not valid.");
        if (session.Grade!.Stage!.RequiresGuardianContact && (guardianName is null || guardianPhone is null))
            throw new DomainException(BookingErrors.GuardianRequired, "Guardian name and WhatsApp number are required for this grade.");

        var currentPrice = SessionPricing.CurrentPrice(session);
        if (currentPrice != booking.QuotedPrice || currentPrice != req.ExpectedPrice)
        {
            booking.QuotedPrice = currentPrice;
            booking.QuotedWorstCasePrice = SessionPricing.GroupTerms(session)?.WorstCasePrice;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new DomainException(BookingErrors.PriceChanged, "The price changed. Please review the new price and confirm again.");
        }

        await ExpireStaleAsync(sessionId, now, ct);

        var student = new Student
        {
            Name = name, ContactPhoneE164 = phone, GuardianName = guardianName, GuardianPhoneE164 = guardianPhone,
            CreatedAtUtc = now,
        };
        db.Students.Add(student);
        booking.StudentId = student.Id;
        booking.ContactPhoneE164 = phone;
        booking.TermsAcceptedAtUtc = now;
        booking.TopUpTermsAcceptedAtUtc = session.Mode == SessionMode.Group ? now : null;
        booking.Status = BookingStatus.PendingPayment;
        booking.HoldExpiresAtUtc = null;
        booking.PaymentDueAtUtc = now.AddMinutes(cfg.PaymentProofDeadlineMinutes);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (db.IsUniqueViolation(ex))
        {
            throw new DomainException(BookingErrors.Duplicate, "This WhatsApp number already has an active booking in this session.");
        }
        await tx.CommitAsync(ct);
    }

    public async Task SubmitPaymentProofAsync(
        string token, Guid paymentMethodId, Stream content, long length, string? senderReference, CancellationToken ct)
    {
        var cfg = await settings.GetBookingSettingsAsync(ct);
        if (length <= 0 || length > cfg.MaxProofFileSizeKb * 1024L)
            throw new DomainException(BookingErrors.InvalidFile, $"The image must be smaller than {cfg.MaxProofFileSizeKb / 1024} MB.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > cfg.MaxProofFileSizeKb * 1024L)
            throw new DomainException(BookingErrors.InvalidFile, "The image is too large.");
        var kind = ImageSniffer.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
                   ?? throw new DomainException(BookingErrors.InvalidFile, "Only JPG, PNG or WebP screenshots are accepted.");

        var method = await db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == paymentMethodId && m.IsActive, ct)
                     ?? throw new DomainException("payment.method_not_found", "Payment method not found.");

        var bookingId = await FindIdAsync(token, ct);
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
        var now = clock.UtcNow;
        if (booking.Status != BookingStatus.PendingPayment)
            throw new DomainException(BookingErrors.InvalidState, "This booking is not waiting for payment.");
        if (booking.IsExpired(now))
        {
            booking.Status = BookingStatus.Expired;
            await db.SaveChangesAsync(ct);
            throw new DomainException(BookingErrors.Expired, "The payment deadline passed and the seat was released.");
        }

        buffer.Position = 0;
        var key = await files.SaveAsync(buffer, kind.Extension, ct);
        db.PaymentProofs.Add(new PaymentProof
        {
            BookingId = booking.Id,
            PaymentMethodId = method.Id,
            Amount = booking.QuotedPrice,
            Currency = booking.Currency,
            SenderReference = string.IsNullOrWhiteSpace(senderReference) ? null : senderReference.Trim()[..Math.Min(senderReference.Trim().Length, 100)],
            FileKey = key,
            FileContentType = kind.ContentType,
            Channel = PaymentChannel.Platform,
            SubmittedAtUtc = now,
        });
        booking.Status = BookingStatus.PaymentUnderReview;
        booking.PaymentDueAtUtc = null;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(BookingErrors.InvalidState, "The booking changed while uploading. Please refresh and try again.");
        }
    }

    public async Task<BookingViewDto> GetViewAsync(string token, CancellationToken ct)
    {
        var bookingId = await FindIdAsync(token, ct);
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var now = clock.UtcNow;

        var booking = await db.Bookings.AsNoTracking().Include(b => b.Student).SingleAsync(b => b.Id == bookingId, ct);
        var session = await LoadSessionAsync(booking.SessionId, ct, tracking: false)
                      ?? throw new DomainException(BookingErrors.SessionNotFound, "Session not found.");
        var latestProof = await db.PaymentProofs.AsNoTracking().Where(p => p.BookingId == bookingId)
            .OrderByDescending(p => p.SubmittedAtUtc).FirstOrDefaultAsync(ct);

        var counts = await new SessionQueries(db, clock).CountsAsync([session.Id], ct);
        var status = booking.IsExpired(now) ? BookingStatus.Expired : booking.Status;
        var joinOpens = session.StartUtc.AddMinutes(-cfg.JoinOpensMinutesBeforeStart);
        var canJoin = status == BookingStatus.Confirmed && now >= joinOpens && now <= session.EndUtc && session.MeetingUrl is not null;

        return new BookingViewDto(
            booking.Reference, status.ToString(),
            SessionQueries.ToSummary(session, counts.GetValueOrDefault(session.Id)),
            SessionQueries.ToTeacher(session), SessionPricing.GroupTerms(session),
            session.Grade!.Stage!.RequiresGuardianContact,
            booking.QuotedPrice, booking.Currency, booking.HoldExpiresAtUtc, booking.PaymentDueAtUtc,
            booking.Student?.Name, latestProof?.Status.ToString(), latestProof?.RejectionReason,
            canJoin, status == BookingStatus.Confirmed ? joinOpens : null,
            $"مرحباً Dose، رقم حجزي {booking.Reference}");
    }

    /// <summary>Returns the meeting URL only for confirmed bookings inside the join window.</summary>
    public async Task<JoinResponse> JoinAsync(string token, CancellationToken ct)
    {
        var bookingId = await FindIdAsync(token, ct);
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var now = clock.UtcNow;
        var row = await db.Bookings.AsNoTracking().Where(b => b.Id == bookingId)
            .Select(b => new { b.Status, b.Session!.StartUtc, b.Session.EndUtc, b.Session.MeetingUrl, SessionStatus = b.Session.Status })
            .SingleAsync(ct);

        if (row.Status != BookingStatus.Confirmed || row.SessionStatus == SessionStatus.Cancelled)
            throw new DomainException(BookingErrors.JoinNotAvailable, "Only confirmed bookings can join the session.");
        if (now < row.StartUtc.AddMinutes(-cfg.JoinOpensMinutesBeforeStart))
            throw new DomainException(BookingErrors.JoinNotAvailable, "The session link opens shortly before the start time.");
        if (now > row.EndUtc)
            throw new DomainException(BookingErrors.JoinNotAvailable, "This session has ended.");
        if (string.IsNullOrWhiteSpace(row.MeetingUrl))
            throw new DomainException(BookingErrors.JoinNotAvailable, "The meeting link has not been added yet. Please contact us on WhatsApp.");
        return new JoinResponse(row.MeetingUrl);
    }

    private async Task<Guid> FindIdAsync(string token, CancellationToken ct)
    {
        if (!AccessTokens.LooksValid(token)) throw new DomainException(BookingErrors.NotFound, "Booking not found.");
        var hash = AccessTokens.Hash(token);
        var id = await db.Bookings.Where(b => b.AccessTokenHash == hash).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        return id ?? throw new DomainException(BookingErrors.NotFound, "Booking not found.");
    }

    private Task<Session?> LoadSessionAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = db.Sessions.Include(s => s.Grade!).ThenInclude(g => g.Stage)
            .Include(s => s.Teacher).Include(s => s.Subject).Include(s => s.StudyTrack).Include(s => s.ServiceType)
            .Include(s => s.TargetTier)
            .Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Plan)
            .Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Tiers)
            .AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    /// <summary>Marks expired holds/unpaid bookings so they stop blocking seats and duplicate checks.</summary>
    private async Task ExpireStaleAsync(Guid sessionId, DateTime now, CancellationToken ct)
    {
        await db.Bookings
            .Where(b => b.SessionId == sessionId &&
                        ((b.Status == BookingStatus.PendingHold && b.HoldExpiresAtUtc <= now) ||
                         (b.Status == BookingStatus.PendingPayment && b.PaymentDueAtUtc <= now)))
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.Status, BookingStatus.Expired), ct);
    }
}
