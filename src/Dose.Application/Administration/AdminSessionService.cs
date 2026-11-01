using Dose.Application.Abstractions;
using Dose.Application.Bookings;
using Dose.Application.Sessions;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Administration;

/// <summary>
/// Admin session management. Pricing is resolved from the configured plans when the session is saved and
/// the price version is locked, so later price edits never change an existing session (spec 6.5, 7).
/// </summary>
public sealed class AdminSessionService(IDoseDbContext db, IClock clock, SettingsService settings, Notifications.NotificationService notify, ITokenProtector tokens)
{
    public async Task<PricingPreviewDto> PreviewPricingAsync(PricingPreviewRequest req, CancellationToken ct)
    {
        var (plan, version) = await ResolvePricingAsync(req.ServiceTypeId, req.GradeId, req.StudyTrackId, req.SubjectId, ParseMode(req.Mode), ct);
        return new PricingPreviewDto(plan.Id, plan.Name, version.Id, version.VersionNo, plan.Currency, version.PrivatePrice,
            version.Tiers.Where(t => t.IsActive).OrderBy(t => t.MinParticipants)
                .Select(t => new TierAdminDto(t.Id, t.NameAr, t.MinParticipants, t.MaxParticipants, t.PricePerStudent, t.SortOrder, t.NameEn)).ToList());
    }

    public async Task<IReadOnlyList<SessionAdminDto>> ListAsync(string? status, bool includePast, CancellationToken ct)
    {
        var q = Query();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SessionStatus>(status, out var st)) q = q.Where(s => s.Status == st);
        if (!includePast) { var cutoff = clock.UtcNow.AddDays(-1); q = q.Where(s => s.EndUtc >= cutoff); }
        var sessions = await q.OrderBy(s => s.StartUtc).Take(300).ToListAsync(ct);
        return await ToDtosAsync(sessions, ct);
    }

    public async Task<SessionAdminDto> GetAsync(Guid id, CancellationToken ct)
    {
        var s = AdminGuard.Found(await Query().FirstOrDefaultAsync(x => x.Id == id, ct), Fields.Session);
        return (await ToDtosAsync([s], ct))[0];
    }

    public async Task<Guid> CreateAsync(SessionSaveRequest req, string actor, CancellationToken ct)
    {
        var session = new Session { ScopeTitleAr = "" };
        await ApplyAsync(session, req, hasBookings: false, ct);
        session.Status = SessionStatus.Draft;
        db.Sessions.Add(session);
        db.Audit(clock, actor, nameof(Session), session.Id, "Create", req);
        await db.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task UpdateAsync(Guid id, SessionSaveRequest req, string actor, CancellationToken ct)
    {
        var session = AdminGuard.Found(await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct), Fields.Session);
        if (session.Status is SessionStatus.Cancelled or SessionStatus.Completed)
            throw new DomainException(AdminErrors.Invalid, "مينفعش تعديل جلسة ملغية أو منتهية.", "A cancelled or completed session can't be edited.");
        var hasBookings = await ActiveBookings(id).AnyAsync(ct);
        var before = new { session.TeacherId, session.StartUtc, session.EndUtc, session.MeetingUrl };
        await ApplyAsync(session, req, hasBookings, ct);
        db.Audit(clock, actor, nameof(Session), id, "Update", new { before, after = req });
        await db.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(Guid id, string actor, CancellationToken ct)
    {
        var s = AdminGuard.Found(await db.Sessions.Include(x => x.Teacher).FirstOrDefaultAsync(x => x.Id == id, ct), Fields.Session);
        if (s.Status != SessionStatus.Draft) throw new DomainException(AdminErrors.Invalid, "الجلسة دي مش مسودة.", "This session is not a draft.");
        if (!s.Teacher!.IsPublished) throw new DomainException(AdminErrors.Invalid, "ملف المدرس مش منشور. انشره الأول من صفحة المدرسين.", "The teacher profile isn't published. Publish it first from the Teachers page.");
        if (s.BookingDeadlineUtc <= clock.UtcNow) throw new DomainException(AdminErrors.Invalid, "آخر ميعاد للحجز عدّى. غيّر ميعاد الجلسة.", "The booking deadline has passed. Change the session time.");
        s.Status = SessionStatus.Published;
        db.Audit(clock, actor, nameof(Session), id, "Publish");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Dose-initiated cancellation: unpaid bookings are cancelled; paid ones move to RefundPending (full refund).</summary>
    public async Task<int> CancelAsync(Guid id, string reason, string actor, CancellationToken ct)
    {
        reason = AdminGuard.Name(reason, Fields.CancelReason, 300);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSessionAsync(id, ct);
        var s = AdminGuard.Found(await db.Sessions.FirstOrDefaultAsync(x => x.Id == id, ct), Fields.Session);
        if (s.Status is SessionStatus.Cancelled or SessionStatus.Completed)
            throw new DomainException(AdminErrors.Invalid, "الجلسة ملغية أو منتهية بالفعل.", "The session is already cancelled or completed.");

        var bookings = await db.Bookings.Where(b => b.SessionId == id && BookingStatuses.SeatOccupying.Contains(b.Status)).ToListAsync(ct);
        foreach (var b in bookings)
            b.Status = b.Status is BookingStatus.PendingHold or BookingStatus.PendingPayment ? BookingStatus.Cancelled : BookingStatus.RefundPending;
        s.Status = SessionStatus.Cancelled;
        foreach (var b in bookings)
            await notify.QueueForBookingAsync(b.Id, Domain.Notifications.TemplateCodes.SessionCancelled, $"cancelled:{b.Id}", ct,
                new Dictionary<string, string> { ["reason"] = reason });
        db.Audit(clock, actor, nameof(Session), id, "Cancel", new { reason, affected = bookings.Select(b => new { b.Reference, status = b.Status.ToString() }) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return bookings.Count(b => b.Status == BookingStatus.RefundPending);
    }

    public async Task<IReadOnlyList<SessionBookingAdminDto>> BookingsAsync(Guid sessionId, CancellationToken ct) =>
        await db.Bookings.AsNoTracking().Where(b => b.SessionId == sessionId).OrderBy(b => b.CreatedAtUtc)
            .Select(b => new SessionBookingAdminDto(b.Id, b.Reference, b.Status.ToString(), b.Student!.Name, b.Student.ContactPhoneE164,
                b.Student.GuardianName, b.Student.GuardianPhoneE164, b.QuotedPrice, b.Currency, b.CreatedAtUtc, b.ConfirmedAtUtc))
            .ToListAsync(ct);

    /// <summary>
    /// Issues a new secret link for a booking (the old link stops working). Only the hash is stored, so this is
    /// how an admin can send a student their link on WhatsApp.
    /// </summary>
    public async Task<ReissueLinkResponse> ReissueLinkAsync(Guid bookingId, string actor, CancellationToken ct)
    {
        var b = AdminGuard.Found(await db.Bookings.Include(x => x.Student).FirstOrDefaultAsync(x => x.Id == bookingId, ct), Fields.Booking);
        var (token, hash) = AccessTokens.Create();
        b.AccessTokenHash = hash;
        b.AccessTokenProtected = tokens.Protect(token);
        db.Audit(clock, actor, nameof(Booking), b.Id, "ReissueLink", new { b.Reference });
        await db.SaveChangesAsync(ct);
        var greeting = b.Student is null ? "أهلاً" : $"أهلاً {b.Student.Name}";
        return new ReissueLinkResponse(b.Reference, token, b.Student?.ContactPhoneE164,
            $"{greeting}، ده لينك حجزك في Dose (رقم الحجز {b.Reference}). من اللينك ده تتابع الحجز وتدخل الجلسة في ميعادها:");
    }

    // ---------- helpers ----------

    private IQueryable<Session> Query() => db.Sessions.AsNoTracking()
        .Include(s => s.Teacher).Include(s => s.Subject).Include(s => s.Grade).Include(s => s.StudyTrack).Include(s => s.ServiceType)
        .Include(s => s.TargetTier).Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Plan)
        .AsSplitQuery();

    private IQueryable<Booking> ActiveBookings(Guid sessionId) =>
        db.Bookings.Where(b => b.SessionId == sessionId).Where(SeatRules.OccupiesSeat(clock.UtcNow));

    private async Task<List<SessionAdminDto>> ToDtosAsync(List<Session> sessions, CancellationToken ct)
    {
        var ids = sessions.Select(s => s.Id).ToList();
        var counts = await new SessionQueries(db, clock).CountsAsync(ids, ct);
        var active = await db.Bookings.Where(b => ids.Contains(b.SessionId) && BookingStatuses.SeatOccupying.Contains(b.Status))
            .GroupBy(b => b.SessionId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return sessions.Select(s =>
        {
            var c = counts.GetValueOrDefault(s.Id);
            var price = s.Mode == SessionMode.Private ? s.PricingPlanVersion!.PrivatePrice ?? 0 : s.TargetTier?.PricePerStudent ?? 0;
            return new SessionAdminDto(s.Id, s.Status.ToString(), s.ScopeTitleAr, s.ScopeDetailsAr,
                s.ServiceTypeId, s.ServiceType!.NameAr, s.GradeId, s.Grade!.NameAr, s.StudyTrackId, s.StudyTrack!.NameAr,
                s.SubjectId, s.Subject!.NameAr, s.ChapterId, s.LessonId, s.TeacherId, s.Teacher!.DisplayNameAr,
                s.StartUtc, s.EndUtc, (int)(s.EndUtc - s.StartUtc).TotalMinutes, s.Mode.ToString(), s.Capacity, s.MinParticipants,
                s.TargetTierId, s.TargetTier?.NameAr, price, s.PricingPlanVersion!.Plan!.Currency, s.PricingPlanVersion.Plan.Name,
                s.BookingDeadlineUtc, s.TierDecisionAtUtc, s.MeetingUrl,
                c.Confirmed, c.Occupied, active.GetValueOrDefault(s.Id), s.ScopeTitleEn, s.ScopeDetailsEn);
        }).ToList();
    }

    private static SessionMode ParseMode(string? mode) =>
        Enum.TryParse<SessionMode>(mode, out var m) ? m : throw new DomainException(AdminErrors.Invalid, "اختار فردي أو جماعي.", "Choose private or group.");

    private async Task<(PricingPlan Plan, PricingPlanVersion Version)> ResolvePricingAsync(
        Guid serviceTypeId, Guid gradeId, Guid trackId, Guid subjectId, SessionMode mode, CancellationToken ct)
    {
        var grade = AdminGuard.Found(await db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gradeId, ct), Fields.Grade);
        var plans = await db.PricingPlans.Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .Where(p => p.IsActive && p.ServiceTypeId == serviceTypeId && p.Mode == mode).AsSplitQuery().ToListAsync(ct);
        try
        {
            var plan = PricingPlanResolver.Resolve(plans, new PricingContext(serviceTypeId, mode, grade.PricingBandId, trackId, subjectId, clock.UtcNow));
            return (plan, plan.CurrentVersion!);
        }
        catch (DomainException ex) when (ex.Code == PricingErrors.NotConfigured)
        {
            throw new DomainException(PricingErrors.NotConfigured, "مفيش خطة تسعير مناسبة للصف والمنهج والمادة ونوع الخدمة دول. أضف خطة من صفحة الأسعار الأول.", "No pricing plan matches this grade, track, subject and service type. Add one from the Pricing page first.");
        }
    }

    private async Task ApplyAsync(Session s, SessionSaveRequest req, bool hasBookings, CancellationToken ct)
    {
        var mode = ParseMode(req.Mode);
        var now = clock.UtcNow;
        var cfg = await settings.GetBookingSettingsAsync(ct);

        if (!await db.ServiceTypes.AnyAsync(x => x.Id == req.ServiceTypeId && x.IsActive, ct)) throw new DomainException(AdminErrors.Invalid, "اختار نوع الخدمة.", "Choose the service type.");
        if (!await db.StudyTracks.AnyAsync(x => x.Id == req.StudyTrackId, ct)) throw new DomainException(AdminErrors.Invalid, "اختار المنهج.", "Choose the curriculum track.");
        if (!await db.GradeSubjects.AnyAsync(x => x.GradeId == req.GradeId && x.SubjectId == req.SubjectId, ct))
            throw new DomainException(AdminErrors.Invalid, "المادة دي مش مربوطة بالصف ده. اربطها من صفحة الصفوف.", "This subject isn't linked to this grade. Link it from the Grades page.");
        if (!await db.Teachers.AnyAsync(x => x.Id == req.TeacherId, ct)) throw new DomainException(AdminErrors.Invalid, "اختار المدرس.", "Choose the teacher.");
        if (req.ChapterId is { } chId && !await db.Chapters.AnyAsync(c => c.Id == chId && c.GradeId == req.GradeId && c.SubjectId == req.SubjectId, ct))
            throw new DomainException(AdminErrors.Invalid, "الوحدة مش تابعة للصف والمادة دول.", "The chapter doesn't belong to this grade and subject.");
        if (req.LessonId is { } lId && !await db.Lessons.AnyAsync(l => l.Id == lId && l.ChapterId == req.ChapterId, ct))
            throw new DomainException(AdminErrors.Invalid, "الدرس مش تابع للوحدة المختارة.", "The lesson doesn't belong to the chosen chapter.");
        if (req.DurationMinutes is < 15 or > 480) throw new DomainException(AdminErrors.Invalid, "مدة الجلسة لازم تكون بين 15 دقيقة و8 ساعات.", "Session duration must be between 15 minutes and 8 hours.");
        var start = DateTime.SpecifyKind(req.StartUtc, DateTimeKind.Utc);
        if (start <= now.AddHours(1)) throw new DomainException(AdminErrors.Invalid, "ميعاد الجلسة لازم يكون بعد ساعة على الأقل من دلوقتي.", "The session must start at least one hour from now.");

        var pricingChanged = s.PricingPlanVersionId == Guid.Empty || s.Mode != mode || s.TargetTierId != req.TargetTierId ||
                             s.GradeId != req.GradeId || s.SubjectId != req.SubjectId || s.StudyTrackId != req.StudyTrackId ||
                             s.ServiceTypeId != req.ServiceTypeId;
        if (hasBookings && pricingChanged)
            throw new DomainException(AdminErrors.Conflict, "فيه حجوزات في الجلسة دي، فمينفعش تغيّر الصف أو المادة أو المنهج أو النوع أو حجم الجروب. تقدر تغيّر الميعاد والمدرس والرابط بس.", "This session has bookings, so grade, subject, track, type and group size can't change. You can still change the time, teacher and link.");

        if (pricingChanged)
        {
            var (_, version) = await ResolvePricingAsync(req.ServiceTypeId, req.GradeId, req.StudyTrackId, req.SubjectId, mode, ct);
            if (mode == SessionMode.Group)
            {
                var tier = version.Tiers.FirstOrDefault(t => t.Id == req.TargetTierId && t.IsActive)
                           ?? throw new DomainException(AdminErrors.Invalid, "اختار حجم الجروب من شرائح خطة التسعير.", "Choose a group size from the pricing plan's options.");
                s.TargetTierId = tier.Id;
                s.Capacity = tier.MaxParticipants;
                s.MinParticipants = tier.MinParticipants;
            }
            else
            {
                if (version.PrivatePrice is null) throw new DomainException(PricingErrors.NotConfigured, "خطة التسعير مالهاش سعر للجلسة الفردية.", "The pricing plan has no private session price.");
                s.TargetTierId = null;
                s.Capacity = 1;
                s.MinParticipants = 1;
            }
            s.PricingPlanVersionId = version.Id;
            s.Policy = version.Plan!.Policy;
            version.IsLocked = true; // referenced by a session from now on
        }

        s.Mode = mode;
        s.ServiceTypeId = req.ServiceTypeId;
        s.GradeId = req.GradeId;
        s.StudyTrackId = req.StudyTrackId;
        s.SubjectId = req.SubjectId;
        s.ChapterId = req.ChapterId;
        s.LessonId = req.ChapterId is null ? null : req.LessonId;
        s.ScopeTitleAr = AdminGuard.Name(req.ScopeTitleAr, Fields.SessionTitle, 200);
        s.ScopeDetailsAr = AdminGuard.Optional(req.ScopeDetailsAr, 2000);
        s.ScopeTitleEn = AdminGuard.Optional(req.ScopeTitleEn, 200);
        s.ScopeDetailsEn = AdminGuard.Optional(req.ScopeDetailsEn, 2000);
        s.TeacherId = req.TeacherId;
        s.StartUtc = start;
        s.EndUtc = start.AddMinutes(req.DurationMinutes);
        s.MeetingUrl = AdminGuard.Url(req.MeetingUrl);
        s.MeetingProvider = s.MeetingUrl is null ? null
            : s.MeetingUrl.Contains("meet.google.com", StringComparison.OrdinalIgnoreCase) ? "GoogleMeet" : "Other";

        if (mode == SessionMode.Group)
        {
            s.TierDecisionAtUtc = start.AddHours(-cfg.TierDecisionHoursBeforeStart);
            s.BookingDeadlineUtc = s.TierDecisionAtUtc.Value;
        }
        else
        {
            s.TierDecisionAtUtc = null;
            s.BookingDeadlineUtc = start.AddHours(-cfg.PrivateBookingDeadlineHours);
        }
        if (s.BookingDeadlineUtc <= now)
            throw new DomainException(AdminErrors.Invalid, "الميعاد قريب جداً: آخر ميعاد للحجز هيكون عدّى. اختار ميعاد أبعد أو غيّر المهلة من الإعدادات.", "Too soon: the booking deadline would already have passed. Pick a later time or change the deadline in Settings.");
    }
}
