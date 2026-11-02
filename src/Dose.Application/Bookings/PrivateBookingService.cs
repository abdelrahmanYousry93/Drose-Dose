using System.Security.Cryptography;
using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Application.Sessions;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Bookings;

/// <summary>Short codes friends type to join a private group (no 0/O, 1/I/L to avoid mix-ups).</summary>
public static class InviteCodes
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string New() => string.Create(8, 0, (span, _) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
    });

    public static async Task<string> NewUniqueAsync(IDoseDbContext db, CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var code = New();
            if (!await db.Bookings.AnyAsync(b => b.InviteCode == code, ct)) return code;
        }
        throw new InvalidOperationException("Could not allocate an invite code.");
    }

    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
}

/// <summary>
/// Sessions a student books for themselves: a private session (size 1) or a private group of friends (size N) where the
/// organizer pays for all N seats at the group price for N and shares an invite code. Times come from the free time the
/// teachers of that subject have had approved; when nothing suits, the student sends a request and the team offers a
/// time (see <see cref="OfferAsync"/>). The session is hidden from public lists and has no group-size decision.
/// </summary>
public sealed class PrivateBookingService(
    IDoseDbContext db, IClock clock, SettingsService settings, IRequestLanguage lang, ITokenProtector tokens,
    Notifications.NotificationService notify)
{
    private const int SlotStepMinutes = 30;
    private const int LookAheadDays = 14;

    private sealed record Pricing(PricingPlan Plan, PricingPlanVersion Version, PricingTier? Tier, decimal PerStudent, int MaxSize);

    private static DomainException Invalid(string ar, string en) => new(AdminErrors.Invalid, ar, en);

    private async Task<Pricing> PriceForSizeAsync(Guid serviceTypeId, Guid gradeId, Guid trackId, Guid subjectId, int size, CancellationToken ct)
    {
        var grade = await db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gradeId && g.IsActive, ct)
                    ?? throw Invalid("اختار الصف.", "Choose the grade.");
        var plans = await db.PricingPlans.AsNoTracking().Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .Where(p => p.IsActive && p.ServiceTypeId == serviceTypeId).AsSplitQuery().ToListAsync(ct);
        PricingPlan? Resolve(SessionMode mode)
        {
            try { return PricingPlanResolver.Resolve(plans, new PricingContext(serviceTypeId, mode, grade.PricingBandId, trackId, subjectId, clock.UtcNow)); }
            catch (DomainException) { return null; }
        }
        var group = Resolve(SessionMode.Group);
        var maxSize = group?.CurrentVersion?.Tiers.Where(t => t.IsActive).Select(t => t.MaxParticipants).DefaultIfEmpty(1).Max() ?? 1;

        if (size == 1)
        {
            var priv = Resolve(SessionMode.Private);
            if (priv?.CurrentVersion?.PrivatePrice is not { } p)
                throw new DomainException(PricingErrors.NotConfigured, "سعر الجلسة الخاصة لسه متحددش للمادة دي. ابعت طلب وهنرتّبها معاك.", "The private session price isn't set for this subject yet. Send a request and we'll arrange it.");
            return new Pricing(priv, priv.CurrentVersion, null, p, maxSize);
        }
        var version = group?.CurrentVersion
                      ?? throw new DomainException(PricingErrors.NotConfigured, "أسعار المجموعات لسه متحددتش للمادة دي. ابعت طلب وهنرتّبها معاك.", "Group prices aren't set for this subject yet. Send a request and we'll arrange it.");
        var tier = version.Tiers.Where(t => t.IsActive).FirstOrDefault(t => size >= t.MinParticipants && size <= t.MaxParticipants)
                   ?? throw Invalid($"حجم المجموعة لازم يكون من 2 لـ {maxSize}.", $"The group size must be between 2 and {maxSize}.");
        return new Pricing(group!, version, tier, tier.PricePerStudent, maxSize);
    }

    /// <summary>Free start times (next 14 days) of published teachers who teach the subject, with the price for the size.</summary>
    public async Task<SlotsDto> SlotsAsync(Guid gradeId, Guid subjectId, Guid trackId, Guid serviceTypeId, int size, CancellationToken ct)
    {
        if (size is < 1 or > 100) throw Invalid("اختار عدد الطلاب.", "Choose the number of students.");
        await ValidateCatalogAsync(gradeId, subjectId, trackId, serviceTypeId, ct);
        var pricing = await PriceForSizeAsync(serviceTypeId, gradeId, trackId, subjectId, size, ct);
        var duration = await DurationAsync(serviceTypeId, ct);
        var now = clock.UtcNow;
        var from = now.AddHours(await settings.GetIntAsync(SettingKeys.PrivateMinLeadHours, ct));
        var to = now.AddDays(LookAheadDays);

        var teachers = await (from ts in db.TeacherSubjects
                              join t in db.Teachers on ts.TeacherId equals t.Id
                              where ts.SubjectId == subjectId && t.IsPublished
                              select t).AsNoTracking().ToListAsync(ct);
        var ids = teachers.Select(t => t.Id).ToList();
        var windows = await db.TeacherAvailabilities.AsNoTracking()
            .Where(a => ids.Contains(a.TeacherId) && a.Status == Domain.Teachers.AvailabilityStatus.Approved && a.EndUtc > from && a.StartUtc < to)
            .ToListAsync(ct);
        var busy = await BusyAsync(ids, from, to, null, ct);

        var slots = new List<SlotDto>();
        foreach (var w in windows.OrderBy(w => w.StartUtc))
        {
            var t = teachers.First(x => x.Id == w.TeacherId);
            for (var start = RoundUp(Max(w.StartUtc, from)); start.AddMinutes(duration) <= w.EndUtc && start < to; start = start.AddMinutes(SlotStepMinutes))
            {
                var end = start.AddMinutes(duration);
                if (busy.Any(b => b.TeacherId == t.Id && b.StartUtc < end && b.EndUtc > start)) continue;
                slots.Add(new SlotDto(t.Id, lang.Pick(t.DisplayNameAr, t.DisplayNameEn), SessionQueries.PhotoUrl(t), start));
                if (slots.Count >= 300) break;
            }
        }
        return new SlotsDto(size, pricing.MaxSize, duration, pricing.PerStudent, pricing.PerStudent * size,
            pricing.Plan.Currency, slots.OrderBy(s => s.StartUtc).ThenBy(s => s.TeacherName).ToList());
    }

    /// <summary>Books a free time: creates the private session and the organizer's seat hold (paid for all seats).</summary>
    public async Task<CreateHoldResponse> CreateAsync(CreatePrivateBookingRequest req, CancellationToken ct)
    {
        await ValidateCatalogAsync(req.GradeId, req.SubjectId, req.StudyTrackId, req.ServiceTypeId, ct);
        if (req.Size is < 1 or > 100) throw Invalid("اختار عدد الطلاب.", "Choose the number of students.");
        var pricing = await PriceForSizeAsync(req.ServiceTypeId, req.GradeId, req.StudyTrackId, req.SubjectId, req.Size, ct);
        var duration = await DurationAsync(req.ServiceTypeId, ct);
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var now = clock.UtcNow;
        var start = DateTime.SpecifyKind(req.StartUtc, DateTimeKind.Utc);
        var end = start.AddMinutes(duration);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockTeacherAsync(req.TeacherId, ct);

        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == req.TeacherId && t.IsPublished, ct);
        var teaches = teacher is not null && await db.TeacherSubjects.AnyAsync(x => x.TeacherId == req.TeacherId && x.SubjectId == req.SubjectId, ct);
        var inWindow = await db.TeacherAvailabilities.AnyAsync(a => a.TeacherId == req.TeacherId
            && a.Status == Domain.Teachers.AvailabilityStatus.Approved && a.StartUtc <= start && a.EndUtc >= end, ct);
        var lead = await settings.GetIntAsync(SettingKeys.PrivateMinLeadHours, ct);
        var free = (await BusyAsync([req.TeacherId], start, end, null, ct)).Count == 0;
        if (!teaches || !inWindow || !free || start < now.AddHours(lead) || start.Minute % SlotStepMinutes != 0)
            throw new DomainException(BookingErrors.SessionFull, "الميعاد ده اتحجز أو مبقاش متاح. اختار ميعاد تاني.", "That time was just taken or is no longer available. Please pick another.");

        var subject = await db.Subjects.AsNoTracking().SingleAsync(s => s.Id == req.SubjectId, ct);
        var scope = AdminGuard.Optional(req.ScopeText, 200);
        var session = NewClosedSession(req.ServiceTypeId, req.GradeId, req.StudyTrackId, req.SubjectId, req.TeacherId, start, end,
            req.Size, pricing, scope ?? DefaultTitleAr(subject.NameAr, req.Size), scope ?? DefaultTitleEn(subject.NameEn ?? subject.NameAr, req.Size),
            start.AddHours(-cfg.PrivateBookingDeadlineHours));
        if (session.BookingDeadlineUtc <= now) session.BookingDeadlineUtc = start;
        db.Sessions.Add(session);
        await db.PricingPlanVersions.Where(v => v.Id == pricing.Version.Id).ExecuteUpdateAsync(u => u.SetProperty(v => v.IsLocked, true), ct);

        var (booking, token) = NewOrganizerBooking(session, req.Size, pricing, now.AddMinutes(cfg.HoldMinutes), lang.Code);
        db.Bookings.Add(booking);
        db.Audit(clock, "student", nameof(Session), session.Id, "StudentBookedPrivate", new { req.Size, req.TeacherId, start });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new CreateHoldResponse(booking.Id, booking.Reference, token, booking.HoldExpiresAtUtc!.Value, booking.QuotedPrice, null, booking.Currency);
    }

    /// <summary>
    /// Admin answers a private session / private group request with a time: creates the session and a booking already
    /// filled with the student's details, and queues a WhatsApp message with the link to confirm and pay.
    /// </summary>
    public async Task<OfferResponse> OfferAsync(Guid requestId, OfferRequest req, string actor, CancellationToken ct)
    {
        var r = AdminGuard.Found(await db.SessionRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct), Fields.Request);
        var size = r.GroupSize ?? (r.Mode == SessionMode.Private ? 1 : 0);
        if (size < 1)
            throw Invalid("ده طلب جروب مفتوح: اعمل جلسة جماعية من صفحة الجلسات واربطها بالطلب.", "This is an open-group request: create a group session from the Sessions page and link it to the request.");
        var pricing = await PriceForSizeAsync(r.ServiceTypeId, r.GradeId, r.StudyTrackId, r.SubjectId, size, ct);
        var teacher = await db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TeacherId, ct)
                      ?? throw Invalid("اختار المدرس.", "Choose the teacher.");
        if (!teacher.IsPublished) throw Invalid("ملف المدرس مش منشور. انشره الأول من صفحة المدرسين.", "The teacher profile isn't published. Publish it first from the Teachers page.");
        var duration = req.DurationMinutes ?? await DurationAsync(r.ServiceTypeId, ct);
        if (duration is < 15 or > 480) throw Invalid("مدة الجلسة لازم تكون بين 15 دقيقة و8 ساعات.", "Session duration must be between 15 minutes and 8 hours.");
        var cfg = await settings.GetBookingSettingsAsync(ct);
        var now = clock.UtcNow;
        var start = DateTime.SpecifyKind(req.StartUtc, DateTimeKind.Utc);
        var end = start.AddMinutes(duration);
        if (start <= now.AddHours(2)) throw Invalid("الميعاد لازم يكون بعد ساعتين على الأقل من دلوقتي.", "The time must be at least two hours from now.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockTeacherAsync(req.TeacherId, ct);
        if ((await BusyAsync([req.TeacherId], start, end, null, ct)).Count > 0)
            throw new DomainException(AdminErrors.Conflict, "المدرس عنده جلسة تانية في الوقت ده.", "The teacher has another session at that time.");

        var subject = await db.Subjects.AsNoTracking().SingleAsync(s => s.Id == r.SubjectId, ct);
        var title = AdminGuard.Optional(req.Title, 200);
        var deadline = start.AddHours(-cfg.PrivateBookingDeadlineHours);
        var session = NewClosedSession(r.ServiceTypeId, r.GradeId, r.StudyTrackId, r.SubjectId, req.TeacherId, start, end, size, pricing,
            title ?? DefaultTitleAr(subject.NameAr, size), title ?? DefaultTitleEn(subject.NameEn ?? subject.NameAr, size),
            deadline > now ? deadline : start);
        db.Sessions.Add(session);
        await db.PricingPlanVersions.Where(v => v.Id == pricing.Version.Id).ExecuteUpdateAsync(u => u.SetProperty(v => v.IsLocked, true), ct);

        var student = new Student
        {
            Name = r.StudentName, ContactPhoneE164 = r.ContactPhoneE164, GuardianName = r.GuardianName, GuardianPhoneE164 = r.GuardianPhoneE164,
            CreatedAtUtc = now,
        };
        db.Students.Add(student);
        var holdUntil = Min(now.AddHours(await settings.GetIntAsync(SettingKeys.OfferHoldHours, ct)), session.BookingDeadlineUtc);
        var (booking, _) = NewOrganizerBooking(session, size, pricing, holdUntil, r.Language);
        booking.StudentId = student.Id;
        db.Bookings.Add(booking);

        r.Status = SessionRequestStatus.Contacted;
        r.FulfilledBySessionId = session.Id;
        r.UpdatedAtUtc = now;
        db.Audit(clock, actor, nameof(SessionRequest), r.Id, "Offer", new { session.Id, req.TeacherId, start, size });
        await db.SaveChangesAsync(ct);
        await notify.QueueForBookingAsync(booking.Id, Domain.Notifications.TemplateCodes.SessionOffer, $"offer:{booking.Id}", ct,
            new Dictionary<string, string> { ["amount"] = booking.QuotedPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new OfferResponse(session.Id, booking.Reference);
    }

    // ---------- Invites ----------

    public async Task<InviteInfoDto> InviteInfoAsync(string code, CancellationToken ct)
    {
        var (organizer, session) = await LoadInviteAsync(code, tracking: false, ct);
        var joined = await FriendsCountAsync(organizer.Id, ct);
        var now = clock.UtcNow;
        var seatsLeft = Math.Max(0, organizer.SeatCount - 1 - joined);
        var counts = await new SessionQueries(db, clock).CountsAsync([session.Id], ct);
        return new InviteInfoDto(SessionQueries.ToSummary(session, counts.GetValueOrDefault(session.Id), lang), SessionQueries.ToTeacher(session, lang),
            BookingService.FirstName(organizer.Student?.Name ?? ""), seatsLeft, session.Grade!.Stage!.RequiresGuardianContact,
            seatsLeft > 0 && now < session.StartUtc && session.Status != SessionStatus.Cancelled);
    }

    public async Task<JoinGroupResponse> JoinAsync(string code, JoinGroupRequest req, CancellationToken ct)
    {
        var sessionId = await db.Bookings.Where(b => b.InviteCode == InviteCodes.Normalize(code)).Select(b => (Guid?)b.SessionId).FirstOrDefaultAsync(ct)
                        ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(sessionId, ct);
        var (organizer, session) = await LoadInviteAsync(code, tracking: true, ct);
        var now = clock.UtcNow;
        if (session.Status == SessionStatus.Cancelled || now >= session.StartUtc)
            throw new DomainException(BookingErrors.SessionClosed, "الجلسة دي اتلغت أو بدأت خلاص.", "This session was cancelled or has already started.");
        if (await FriendsCountAsync(organizer.Id, ct) >= organizer.SeatCount - 1)
            throw new DomainException(BookingErrors.SessionFull, "كل أماكن المجموعة اتملت.", "All seats in this group are taken.");
        if (!req.AcceptTerms)
            throw new DomainException(BookingErrors.TermsRequired, "لازم توافق على شروط الحجز.", "You must accept the booking terms.");

        var name = (req.StudentName ?? "").Trim();
        if (name.Length is < 2 or > 100)
            throw new DomainException("booking.invalid_name", "الاسم لازم يكون بين 2 و100 حرف.", "The name must be between 2 and 100 characters.");
        var phone = PhoneNumber.TryNormalize(req.ContactPhone)
                    ?? throw new DomainException(BookingErrors.InvalidPhone, "رقم الواتساب مش صحيح. اكتبه بالشكل ده: 01xxxxxxxxx", "The WhatsApp number is not valid. Use this format: 01xxxxxxxxx");
        string? guardianName = string.IsNullOrWhiteSpace(req.GuardianName) ? null : req.GuardianName.Trim();
        string? guardianPhone = null;
        if (!string.IsNullOrWhiteSpace(req.GuardianPhone))
            guardianPhone = PhoneNumber.TryNormalize(req.GuardianPhone)
                            ?? throw new DomainException(BookingErrors.InvalidPhone, "رقم واتساب ولي الأمر مش صحيح.", "The guardian's WhatsApp number is not valid.");
        if (session.Grade!.Stage!.RequiresGuardianContact && (guardianName is null || guardianPhone is null))
            throw new DomainException(BookingErrors.GuardianRequired, "اسم ورقم واتساب ولي الأمر مطلوبين للمرحلة دي.", "Guardian name and WhatsApp number are required for this grade.");

        var student = new Student { Name = name, ContactPhoneE164 = phone, GuardianName = guardianName, GuardianPhoneE164 = guardianPhone, CreatedAtUtc = now };
        db.Students.Add(student);
        var (token, hash) = AccessTokens.Create();
        var booking = new Booking
        {
            Reference = AccessTokens.NewReference(), SessionId = session.Id, StudentId = student.Id, ContactPhoneE164 = phone,
            Status = BookingStatus.Confirmed, CreatedAtUtc = now, ConfirmedAtUtc = now, TermsAcceptedAtUtc = now,
            SeatCount = 0, SponsorBookingId = organizer.Id, QuotedPrice = 0, ListPrice = 0, Currency = organizer.Currency,
            AccessTokenHash = hash, AccessTokenProtected = tokens.Protect(token), Language = lang.Code,
        };
        db.Bookings.Add(booking);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (db.IsUniqueViolation(ex))
        {
            throw new DomainException(BookingErrors.Duplicate, "الرقم ده منضم للجلسة دي فعلاً.", "This WhatsApp number has already joined this session.");
        }
        await notify.QueueForBookingAsync(booking.Id, Domain.Notifications.TemplateCodes.BookingConfirmed, $"confirmed:{booking.Id}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new JoinGroupResponse(booking.Reference, token);
    }

    // ---------- helpers ----------

    private static DomainException NotFound() =>
        new(BookingErrors.NotFound, "كود الدعوة مش صحيح. اتأكد منه مع صاحبك.", "This invite code isn't valid. Check it with your friend.");

    private async Task<(Booking Organizer, Session Session)> LoadInviteAsync(string code, bool tracking, CancellationToken ct)
    {
        var normalized = InviteCodes.Normalize(code);
        if (normalized.Length != 8) throw NotFound();
        var organizer = await db.Bookings.AsNoTracking().Include(b => b.Student)
            .FirstOrDefaultAsync(b => b.InviteCode == normalized && b.Status == BookingStatus.Confirmed, ct) ?? throw NotFound();
        var q = db.Sessions.Include(s => s.Grade!).ThenInclude(g => g.Stage)
            .Include(s => s.Teacher).Include(s => s.Subject).Include(s => s.StudyTrack).Include(s => s.ServiceType)
            .Include(s => s.TargetTier).Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Plan)
            .Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Tiers).AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        return (organizer, await q.SingleAsync(s => s.Id == organizer.SessionId, ct));
    }

    private Task<int> FriendsCountAsync(Guid organizerId, CancellationToken ct) =>
        db.Bookings.CountAsync(b => b.SponsorBookingId == organizerId && BookingStatuses.SeatOccupying.Contains(b.Status), ct);

    /// <summary>Teacher sessions that block a time: anything not cancelled, except private sessions nobody holds any more.</summary>
    private async Task<List<Session>> BusyAsync(IReadOnlyList<Guid> teacherIds, DateTime from, DateTime to, Guid? exceptSessionId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var live = db.Bookings.Where(SeatRules.OccupiesSeat(now));
        return await db.Sessions.AsNoTracking()
            .Where(s => teacherIds.Contains(s.TeacherId) && s.Status != SessionStatus.Cancelled && s.EndUtc > from && s.StartUtc < to
                        && s.Id != exceptSessionId && (!s.IsClosed || live.Any(b => b.SessionId == s.Id)))
            .ToListAsync(ct);
    }

    private async Task ValidateCatalogAsync(Guid gradeId, Guid subjectId, Guid trackId, Guid serviceTypeId, CancellationToken ct)
    {
        if (!await db.Grades.AnyAsync(g => g.Id == gradeId && g.IsActive, ct)) throw Invalid("اختار الصف.", "Choose the grade.");
        if (!await db.GradeSubjects.AnyAsync(x => x.GradeId == gradeId && x.SubjectId == subjectId, ct)) throw Invalid("اختار المادة.", "Choose the subject.");
        if (!await db.StudyTracks.AnyAsync(x => x.Id == trackId && x.IsActive, ct)) throw Invalid("اختار المنهج.", "Choose the curriculum track.");
        if (!await db.ServiceTypes.AnyAsync(x => x.Id == serviceTypeId && x.IsActive, ct)) throw Invalid("اختار نوع الشرح.", "Choose the type of help.");
    }

    private async Task<int> DurationAsync(Guid serviceTypeId, CancellationToken ct) =>
        Math.Clamp(await db.ServiceTypes.Where(s => s.Id == serviceTypeId).Select(s => s.DefaultDurationMinutes).FirstOrDefaultAsync(ct), 15, 480);

    private static Session NewClosedSession(Guid serviceTypeId, Guid gradeId, Guid trackId, Guid subjectId, Guid teacherId,
        DateTime start, DateTime end, int size, Pricing pricing, string titleAr, string titleEn, DateTime deadline) => new()
    {
        ServiceTypeId = serviceTypeId, GradeId = gradeId, StudyTrackId = trackId, SubjectId = subjectId, TeacherId = teacherId,
        StartUtc = start, EndUtc = end, Mode = size == 1 ? SessionMode.Private : SessionMode.Group,
        Capacity = size, MinParticipants = size, PricingPlanVersionId = pricing.Version.Id, TargetTierId = pricing.Tier?.Id,
        Policy = pricing.Plan.Policy, BookingDeadlineUtc = deadline, Status = SessionStatus.Published, IsClosed = true,
        ScopeTitleAr = titleAr, ScopeTitleEn = titleEn,
    };

    private (Booking Booking, string Token) NewOrganizerBooking(Session session, int size, Pricing pricing, DateTime holdUntil, string language)
    {
        var (token, hash) = AccessTokens.Create();
        var total = pricing.PerStudent * size;
        return (new Booking
        {
            Reference = AccessTokens.NewReference(), SessionId = session.Id, Status = BookingStatus.PendingHold,
            CreatedAtUtc = clock.UtcNow, HoldExpiresAtUtc = holdUntil, SeatCount = size, ListPrice = total, QuotedPrice = total,
            Currency = pricing.Plan.Currency, AccessTokenHash = hash, AccessTokenProtected = tokens.Protect(token), Language = language,
        }, token);
    }

    private static string DefaultTitleAr(string subject, int size) => size == 1 ? $"{subject} — جلسة خاصة" : $"{subject} — مجموعة خاصة ({size})";
    private static string DefaultTitleEn(string subject, int size) => size == 1 ? $"{subject} — private session" : $"{subject} — private group ({size})";
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime RoundUp(DateTime t)
    {
        var minutes = (int)Math.Ceiling((t.Minute + t.Second / 60.0 + t.Millisecond / 60000.0) / SlotStepMinutes) * SlotStepMinutes;
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc).AddMinutes(minutes);
    }
}
