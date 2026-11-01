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
public sealed class BookingService(IDoseDbContext db, IClock clock, SettingsService settings, IFileStorage files, GroupDecisionService decisions)
{
    public async Task<CreateHoldResponse> CreateHoldAsync(Guid sessionId, CancellationToken ct)
    {
        var cfg = await settings.GetBookingSettingsAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);

        var session = await LoadSessionAsync(sessionId, ct)
                      ?? throw new DomainException(BookingErrors.SessionNotFound, "الجلسة مش موجودة.", "Session not found.");
        var now = clock.UtcNow;
        if (!session.IsOpenForBooking(now))
            throw new DomainException(BookingErrors.SessionClosed, "الحجز في الجلسة دي اتقفل.", "This session is no longer open for booking.");

        await ExpireStaleAsync(sessionId, now, ct);
        var occupied = await db.Bookings.Where(b => b.SessionId == sessionId).CountAsync(SeatRules.OccupiesSeat(now), ct);
        if (occupied >= session.Capacity)
            throw new DomainException(BookingErrors.SessionFull, "للأسف الأماكن خلصت في الجلسة دي.", "No seats left in this session.");

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
            throw new DomainException(BookingErrors.InvalidState, "البيانات اتبعتت للحجز ده قبل كده.", "Details were already submitted for this booking.");
        if (booking.IsExpired(now))
        {
            booking.Status = BookingStatus.Expired;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new DomainException(BookingErrors.Expired, "مدة حجز المقعد خلصت. ابدأ الحجز من جديد.", "The seat hold expired. Please start the booking again.");
        }

        var session = await LoadSessionAsync(sessionId, ct) ?? throw new DomainException(BookingErrors.SessionNotFound, "الجلسة مش موجودة.", "Session not found.");
        if (!session.IsOpenForBooking(now))
            throw new DomainException(BookingErrors.SessionClosed, "الحجز في الجلسة دي اتقفل.", "This session is no longer open for booking.");

        if (!req.AcceptTerms)
            throw new DomainException(BookingErrors.TermsRequired, "لازم توافق على شروط الحجز.", "You must accept the booking terms.");
        if (session.Mode == SessionMode.Group && !req.AcceptTopUpTerms)
            throw new DomainException(BookingErrors.TermsRequired, "لازم توافق على شروط الجروب (احتمال فرق لو الجروب مكملش).", "You must accept the group terms (possible difference if the group does not fill).");

        var name = (req.StudentName ?? "").Trim();
        if (name.Length is < 2 or > 100)
            throw new DomainException("booking.invalid_name", "اسم الطالب لازم يكون بين 2 و100 حرف.", "Student name must be between 2 and 100 characters.");
        var phone = PhoneNumber.TryNormalize(req.ContactPhone)
                    ?? throw new DomainException(BookingErrors.InvalidPhone, "رقم الواتساب مش صحيح. اكتبه بالشكل ده: 01xxxxxxxxx", "The WhatsApp number is not valid. Use this format: 01xxxxxxxxx");

        string? guardianName = string.IsNullOrWhiteSpace(req.GuardianName) ? null : req.GuardianName.Trim();
        string? guardianPhone = null;
        if (!string.IsNullOrWhiteSpace(req.GuardianPhone))
            guardianPhone = PhoneNumber.TryNormalize(req.GuardianPhone)
                            ?? throw new DomainException(BookingErrors.InvalidPhone, "رقم واتساب ولي الأمر مش صحيح.", "The guardian's WhatsApp number is not valid.");
        if (session.Grade!.Stage!.RequiresGuardianContact && (guardianName is null || guardianPhone is null))
            throw new DomainException(BookingErrors.GuardianRequired, "اسم ورقم واتساب ولي الأمر مطلوبين للمرحلة دي.", "Guardian name and WhatsApp number are required for this grade.");

        var currentPrice = SessionPricing.CurrentPrice(session);
        if (currentPrice != booking.QuotedPrice || currentPrice != req.ExpectedPrice)
        {
            booking.QuotedPrice = currentPrice;
            booking.QuotedWorstCasePrice = SessionPricing.GroupTerms(session)?.WorstCasePrice;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new DomainException(BookingErrors.PriceChanged, "السعر اتغيّر. راجع السعر الجديد ووافق تاني.", "The price changed. Please review the new price and confirm again.");
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
            throw new DomainException(BookingErrors.Duplicate, "الرقم ده عنده حجز فعلاً في نفس الجلسة.", "This WhatsApp number already has an active booking in this session.");
        }
        await tx.CommitAsync(ct);
    }

    public async Task SubmitPaymentProofAsync(
        string token, Guid paymentMethodId, Stream content, long length, string? senderReference, CancellationToken ct)
    {
        var (buffer, kind) = await ReadImageAsync(content, length, ct);
        using var _ = buffer;
        var method = await FindMethodAsync(paymentMethodId, ct);

        var bookingId = await FindIdAsync(token, ct);
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
        var now = clock.UtcNow;
        if (booking.Status != BookingStatus.PendingPayment)
            throw new DomainException(BookingErrors.InvalidState, "الحجز ده مش منتظر دفع.", "This booking is not waiting for payment.");
        if (booking.IsExpired(now))
        {
            booking.Status = BookingStatus.Expired;
            await db.SaveChangesAsync(ct);
            throw new DomainException(BookingErrors.Expired, "مهلة الدفع خلصت والمقعد اتفك.", "The payment deadline passed and the seat was released.");
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
            throw new DomainException(BookingErrors.InvalidState, "الحجز اتغيّر وإنت بترفع. حدّث الصفحة وجرّب تاني.", "The booking changed while uploading. Please refresh and try again.");
        }
    }

    public async Task<BookingViewDto> GetViewAsync(string token, CancellationToken ct)
    {
        var bookingId = await FindIdAsync(token, ct);
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var now = clock.UtcNow;

        var booking = await db.Bookings.AsNoTracking().Include(b => b.Student).SingleAsync(b => b.Id == bookingId, ct);
        var session = await LoadSessionAsync(booking.SessionId, ct, tracking: false)
                      ?? throw new DomainException(BookingErrors.SessionNotFound, "الجلسة مش موجودة.", "Session not found.");
        var latestProof = await db.PaymentProofs.AsNoTracking().Where(p => p.BookingId == bookingId && p.AdjustmentId == null)
            .OrderByDescending(p => p.SubmittedAtUtc).FirstOrDefaultAsync(ct);
        var topUp = await db.FinancialAdjustments.AsNoTracking()
            .Where(a => a.BookingId == bookingId && a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Pending)
            .OrderByDescending(a => a.CreatedAtUtc).FirstOrDefaultAsync(ct);
        PaymentProof? topUpProof = topUp is null ? null : await db.PaymentProofs.AsNoTracking()
            .Where(p => p.AdjustmentId == topUp.Id).OrderByDescending(p => p.SubmittedAtUtc).FirstOrDefaultAsync(ct);

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
            $"مرحباً Dose، رقم حجزي {booking.Reference}",
            topUp is null ? null : new TopUpViewDto(topUp.Amount, topUp.Currency, topUp.RespondByUtc, topUp.Reason,
                topUp.StudentChoice?.ToString(), topUpProof?.Status.ToString(), topUpProof?.RejectionReason),
            session.StartUtc.AddHours(-cfg.FullRefundCancellationHours),
            CanCancel(status, session, now));
    }

    /// <summary>Student's answer when their group did not fill (spec 6.4). Never charges anything by itself.</summary>
    public async Task ChooseTopUpAsync(string token, string choiceText, CancellationToken ct)
    {
        if (!Enum.TryParse<TopUpChoice>(choiceText, out var choice))
            throw new DomainException("booking.invalid_choice", "اختار: تدفع الفرق، أو تنقل لميعاد تاني، أو تسترد فلوسك.", "Choose: pay the difference, move to another time, or get a refund.");
        var bookingId = await FindIdAsync(token, ct);
        var sessionId = await db.Bookings.Where(b => b.Id == bookingId).Select(b => b.SessionId).SingleAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
        var adj = await db.FinancialAdjustments.FirstOrDefaultAsync(a => a.BookingId == bookingId
            && a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Pending, ct);
        if (booking.Status != BookingStatus.TopUpPending || adj is null)
            throw new DomainException(BookingErrors.NoTopUp, "مفيش فرق مطلوب على الحجز ده.", "There is no top-up due on this booking.");
        if (await db.PaymentProofs.AnyAsync(p => p.AdjustmentId == adj.Id && p.Status == PaymentProofStatus.Submitted, ct))
            throw new DomainException(BookingErrors.InvalidState, "إيصال الفرق تحت المراجعة بالفعل.", "The top-up proof is already under review.");

        var now = clock.UtcNow;
        adj.StudentChoice = choice;
        adj.ChoiceAtUtc = now;
        if (choice != TopUpChoice.PayDifference)
        {
            adj.Status = AdjustmentStatus.Cancelled;
            adj.ResolvedBy = "student";
            adj.ResolvedAtUtc = now;
            adj.ResolutionNote = choice == TopUpChoice.Refund ? "الطالب اختار الاسترداد" : "الطالب اختار النقل لميعاد تاني";
            booking.Status = choice == TopUpChoice.Refund ? BookingStatus.RefundPending : BookingStatus.Transferred;
            booking.CancelledAtUtc = now;
            booking.CancelReason = adj.ResolutionNote;
        }
        db.AuditLogs.Add(new Domain.Administration.AuditLog
        {
            Actor = "student", Entity = nameof(Booking), EntityId = booking.Id.ToString(), Action = "TopUpChoice",
            DataJson = $$"""{"choice":"{{choice}}","amount":{{adj.Amount}}}""", AtUtc = now,
        });
        await db.SaveChangesAsync(ct);
        var session = await db.Sessions.SingleAsync(s => s.Id == sessionId, ct);
        await decisions.RefreshStatusAsync(session, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task SubmitTopUpProofAsync(
        string token, Guid paymentMethodId, Stream content, long length, string? senderReference, CancellationToken ct)
    {
        var (buffer, kind) = await ReadImageAsync(content, length, ct);
        using var _ = buffer;
        var method = await FindMethodAsync(paymentMethodId, ct);
        var bookingId = await FindIdAsync(token, ct);
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
        var adj = await db.FinancialAdjustments.FirstOrDefaultAsync(a => a.BookingId == bookingId
            && a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Pending, ct);
        if (booking.Status != BookingStatus.TopUpPending || adj is null)
            throw new DomainException(BookingErrors.NoTopUp, "مفيش فرق مطلوب على الحجز ده.", "There is no top-up due on this booking.");
        if (await db.PaymentProofs.AnyAsync(p => p.AdjustmentId == adj.Id && p.Status == PaymentProofStatus.Submitted, ct))
            throw new DomainException(BookingErrors.InvalidState, "إيصال الفرق تحت المراجعة بالفعل.", "The top-up proof is already under review.");

        var now = clock.UtcNow;
        adj.StudentChoice = TopUpChoice.PayDifference;
        adj.ChoiceAtUtc ??= now;
        buffer.Position = 0;
        var key = await files.SaveAsync(buffer, kind.Extension, ct);
        db.PaymentProofs.Add(new PaymentProof
        {
            BookingId = booking.Id, AdjustmentId = adj.Id, PaymentMethodId = method.Id, Amount = adj.Amount, Currency = adj.Currency,
            SenderReference = Trim100(senderReference), FileKey = key, FileContentType = kind.ContentType,
            Channel = PaymentChannel.Platform, SubmittedAtUtc = now,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Student cancellation. Unpaid bookings simply release the seat. Paid bookings get a full refund when cancelled
    /// early enough (admin setting); later cancellations need explicit acceptance that no refund is due (decision D8).
    /// </summary>
    public async Task CancelAsync(string token, bool acceptNoRefund, CancellationToken ct)
    {
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var bookingId = await FindIdAsync(token, ct);
        var sessionId = await db.Bookings.Where(b => b.Id == bookingId).Select(b => b.SessionId).SingleAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
        var session = await db.Sessions.SingleAsync(s => s.Id == sessionId, ct);
        var now = clock.UtcNow;
        if (!CanCancel(booking.IsExpired(now) ? BookingStatus.Expired : booking.Status, session, now))
            throw new DomainException(BookingErrors.CancelNotAllowed, "مينفعش تلغي الحجز ده دلوقتي.", "This booking can't be cancelled now.");

        var paid = booking.Status is BookingStatus.PaymentUnderReview or BookingStatus.Confirmed or BookingStatus.TopUpPending;
        var fullRefund = now <= session.StartUtc.AddHours(-cfg.FullRefundCancellationHours);
        if (paid && !fullRefund && !acceptNoRefund)
            throw new DomainException(BookingErrors.CancelNotAllowed, $"الإلغاء قبل الجلسة بأقل من {cfg.FullRefundCancellationHours} ساعة مش بيرجّع الفلوس. أكّد إنك موافق تلغي من غير استرداد.", $"Cancelling less than {cfg.FullRefundCancellationHours} hours before the session is not refundable. Confirm you accept cancelling without a refund.");

        await db.FinancialAdjustments
            .Where(a => a.BookingId == bookingId && a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.Status, AdjustmentStatus.Cancelled)
                .SetProperty(a => a.ResolutionNote, "الطالب لغى الحجز").SetProperty(a => a.ResolvedAtUtc, now), ct);
        booking.Status = paid && fullRefund ? BookingStatus.RefundPending : BookingStatus.Cancelled;
        booking.CancelledAtUtc = now;
        booking.CancelReason = paid ? (fullRefund ? "إلغاء من الطالب — استرداد كامل" : "إلغاء متأخر من الطالب — بدون استرداد") : "إلغاء من الطالب قبل الدفع";
        db.AuditLogs.Add(new Domain.Administration.AuditLog
        {
            Actor = "student", Entity = nameof(Booking), EntityId = booking.Id.ToString(), Action = "StudentCancel",
            DataJson = $$"""{"status":"{{booking.Status}}"}""", AtUtc = now,
        });
        await db.SaveChangesAsync(ct);
        await decisions.RefreshStatusAsync(session, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static bool CanCancel(BookingStatus status, Session session, DateTime now) =>
        now < session.StartUtc && session.Status != SessionStatus.Cancelled && status is BookingStatus.PendingHold
            or BookingStatus.PendingPayment or BookingStatus.PaymentUnderReview or BookingStatus.Confirmed or BookingStatus.TopUpPending;

    private async Task<(MemoryStream Buffer, (string ContentType, string Extension) Kind)> ReadImageAsync(Stream content, long length, CancellationToken ct)
    {
        var cfg = await settings.GetBookingSettingsAsync(ct);
        if (length <= 0 || length > cfg.MaxProofFileSizeKb * 1024L)
            throw new DomainException(BookingErrors.InvalidFile, $"الصورة لازم تكون أصغر من {cfg.MaxProofFileSizeKb / 1024} ميجا.", $"The image must be smaller than {cfg.MaxProofFileSizeKb / 1024} MB.");
        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > cfg.MaxProofFileSizeKb * 1024L)
            throw new DomainException(BookingErrors.InvalidFile, "الصورة حجمها كبير.", "The image is too large.");
        var kind = ImageSniffer.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
                   ?? throw new DomainException(BookingErrors.InvalidFile, "ارفع صورة بصيغة JPG أو PNG أو WebP.", "Only JPG, PNG or WebP images are accepted.");
        return (buffer, kind);
    }

    private async Task<PaymentMethod> FindMethodAsync(Guid id, CancellationToken ct) =>
        await db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct)
        ?? throw new DomainException("payment.method_not_found", "طريقة الدفع مش موجودة.", "Payment method not found.");

    private static string? Trim100(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, 100)];

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
            throw new DomainException(BookingErrors.JoinNotAvailable, "الدخول للجلسة متاح للحجوزات المؤكدة بس.", "Only confirmed bookings can join the session.");
        if (now < row.StartUtc.AddMinutes(-cfg.JoinOpensMinutesBeforeStart))
            throw new DomainException(BookingErrors.JoinNotAvailable, "رابط الجلسة بيتفتح قبل الميعاد بشوية.", "The session link opens shortly before the start time.");
        if (now > row.EndUtc)
            throw new DomainException(BookingErrors.JoinNotAvailable, "الجلسة دي خلصت.", "This session has ended.");
        if (string.IsNullOrWhiteSpace(row.MeetingUrl))
            throw new DomainException(BookingErrors.JoinNotAvailable, "رابط الجلسة لسه متضافش. كلّمنا على واتساب.", "The meeting link has not been added yet. Please contact us on WhatsApp.");
        return new JoinResponse(row.MeetingUrl);
    }

    private async Task<Guid> FindIdAsync(string token, CancellationToken ct)
    {
        if (!AccessTokens.LooksValid(token)) throw new DomainException(BookingErrors.NotFound, "الحجز مش موجود. اتأكد إنك فاتح اللينك الصح.", "Booking not found. Make sure you opened the right link.");
        var hash = AccessTokens.Hash(token);
        var id = await db.Bookings.Where(b => b.AccessTokenHash == hash).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        return id ?? throw new DomainException(BookingErrors.NotFound, "الحجز مش موجود. اتأكد إنك فاتح اللينك الصح.", "Booking not found. Make sure you opened the right link.");
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
