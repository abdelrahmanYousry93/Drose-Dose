using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Sessions;

/// <summary>"No suitable session? Request one" (spec 7). Admins review requests and create sessions manually.</summary>
public sealed class SessionRequestService(IDoseDbContext db, IClock clock, IRequestLanguage lang, Notifications.NotificationService notifications)
{
    private static readonly string[] Days = ["السبت", "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "أي يوم"];
    private static readonly string[] Times = ["صباحاً", "بعد الظهر", "مساءً", "أي وقت"];

    /// <summary>Indicative prices for the request form. Null prices mean "not configured yet — Dose will confirm".</summary>
    public async Task<PriceOptionsDto?> PriceOptionsAsync(Guid serviceTypeId, Guid gradeId, Guid studyTrackId, Guid subjectId, CancellationToken ct)
    {
        var grade = await db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gradeId, ct);
        if (grade is null) return null;
        var plans = await db.PricingPlans.AsNoTracking().Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .Where(p => p.IsActive && p.ServiceTypeId == serviceTypeId).AsSplitQuery().ToListAsync(ct);
        PricingPlan? Resolve(SessionMode mode)
        {
            try { return PricingPlanResolver.Resolve(plans, new PricingContext(serviceTypeId, mode, grade.PricingBandId, studyTrackId, subjectId, clock.UtcNow)); }
            catch (DomainException) { return null; }
        }
        var privatePlan = Resolve(SessionMode.Private);
        var groupPlan = Resolve(SessionMode.Group);
        if (privatePlan is null && groupPlan is null) return null;
        var currency = (groupPlan ?? privatePlan)!.Currency;
        return new PriceOptionsDto(currency, privatePlan?.CurrentVersion?.PrivatePrice,
            groupPlan?.CurrentVersion?.Tiers.Where(t => t.IsActive).OrderBy(t => t.MinParticipants)
                .Select(t => new GroupOptionDto(t.Id, lang.Pick(t.NameAr, t.NameEn), t.MinParticipants, t.MaxParticipants, t.PricePerStudent)).ToList() ?? []);
    }

    public async Task<CreateSessionRequestResponse> CreateAsync(CreateSessionRequest req, CancellationToken ct)
    {
        var name = AdminGuard.Name(req.StudentName, Fields.StudentName);
        var phone = PhoneNumber.TryNormalize(req.ContactPhone)
                    ?? throw new DomainException(BookingErrors.InvalidPhone, "رقم الواتساب مش صحيح.", "The WhatsApp number is not valid.");
        string? guardianPhone = null;
        if (!string.IsNullOrWhiteSpace(req.GuardianPhone))
            guardianPhone = PhoneNumber.TryNormalize(req.GuardianPhone)
                            ?? throw new DomainException(BookingErrors.InvalidPhone, "رقم واتساب ولي الأمر مش صحيح.", "The guardian's WhatsApp number is not valid.");
        var guardianName = AdminGuard.Optional(req.GuardianName, 100);

        var grade = await db.Grades.Include(g => g.Stage).FirstOrDefaultAsync(g => g.Id == req.GradeId && g.IsActive, ct)
                    ?? throw new DomainException(AdminErrors.Invalid, "اختار الصف.", "Choose the grade.");
        if (grade.Stage!.RequiresGuardianContact && (guardianName is null || guardianPhone is null))
            throw new DomainException(BookingErrors.GuardianRequired, "اسم ورقم واتساب ولي الأمر مطلوبين للمرحلة دي.", "Guardian name and WhatsApp number are required for this stage.");
        if (!await db.GradeSubjects.AnyAsync(x => x.GradeId == req.GradeId && x.SubjectId == req.SubjectId, ct))
            throw new DomainException(AdminErrors.Invalid, "اختار المادة.", "Choose the subject.");
        if (!await db.StudyTracks.AnyAsync(x => x.Id == req.StudyTrackId && x.IsActive, ct)) throw new DomainException(AdminErrors.Invalid, "اختار المنهج.", "Choose the curriculum track.");
        if (!await db.ServiceTypes.AnyAsync(x => x.Id == req.ServiceTypeId && x.IsActive, ct)) throw new DomainException(AdminErrors.Invalid, "اختار نوع الشرح.", "Choose the type of help.");
        if (!Enum.TryParse<SessionMode>(req.Mode, out var mode)) throw new DomainException(AdminErrors.Invalid, "اختار فردي أو جماعي.", "Choose private or group.");
        if (req.GroupSize is { } gs && (mode != SessionMode.Group || gs is < 2 or > 100))
            throw new DomainException(AdminErrors.Invalid, "عدد أفراد المجموعة لازم يكون من 2 لـ 100.", "The group size must be between 2 and 100.");
        if (mode == SessionMode.Group && req.PreferredTierId is { } tierId && !await db.PricingTiers.AnyAsync(t => t.Id == tierId, ct))
            throw new DomainException(AdminErrors.Invalid, "حجم الجروب مش موجود.", "Group size option not found.");

        var days = (req.PreferredDays ?? []).Where(Days.Contains).Distinct().ToList();
        var times = (req.PreferredTimes ?? []).Where(Times.Contains).Distinct().ToList();
        if (days.Count == 0 || times.Count == 0) throw new DomainException(AdminErrors.Invalid, "اختار الأيام والأوقات المناسبة.", "Choose the days and times that suit you.");

        var entity = new SessionRequest
        {
            Reference = "RQ-" + AccessTokens.NewReference()[3..],
            StudentName = name, ContactPhoneE164 = phone, GuardianName = guardianName, GuardianPhoneE164 = guardianPhone,
            GradeId = req.GradeId, StudyTrackId = req.StudyTrackId, SubjectId = req.SubjectId, ServiceTypeId = req.ServiceTypeId,
            ScopeText = AdminGuard.Name(req.ScopeText, Fields.Scope, 1000),
            PreferredDays = string.Join("، ", days), PreferredTimes = string.Join("، ", times),
            Mode = mode, PreferredTierId = mode == SessionMode.Group && req.GroupSize is null ? req.PreferredTierId : null, GroupSize = req.GroupSize,
            Notes = AdminGuard.Optional(req.Notes, 1000), CreatedAtUtc = clock.UtcNow, Language = lang.Code,
        };
        db.SessionRequests.Add(entity);
        await notifications.QueueForRequestAsync(entity, Domain.Notifications.TemplateCodes.RequestReceived, ct);
        await db.SaveChangesAsync(ct);
        return new CreateSessionRequestResponse(entity.Reference, lang.IsEnglish
            ? $"Hello Dose, I sent a new session request: {entity.Reference}"
            : $"مرحباً Dose، بعت طلب جلسة جديدة رقم {entity.Reference}");
    }

    public async Task<IReadOnlyList<SessionRequestAdminDto>> ListAsync(string? status, CancellationToken ct)
    {
        var q = db.SessionRequests.AsNoTracking();
        if (Enum.TryParse<SessionRequestStatus>(status, out var st)) q = q.Where(r => r.Status == st);
        var rows = await (from r in q
                          join g in db.Grades on r.GradeId equals g.Id
                          join t in db.StudyTracks on r.StudyTrackId equals t.Id
                          join s in db.Subjects on r.SubjectId equals s.Id
                          join sv in db.ServiceTypes on r.ServiceTypeId equals sv.Id
                          join tier in db.PricingTiers on r.PreferredTierId equals tier.Id into tiers
                          from tier in tiers.DefaultIfEmpty()
                          orderby r.CreatedAtUtc descending
                          select new { r, Grade = g.NameAr, Track = t.NameAr, Subject = s.NameAr, Service = sv.NameAr, Tier = tier == null ? null : tier.NameAr })
            .Take(300).ToListAsync(ct);
        return rows.Select(x => new SessionRequestAdminDto(
            x.r.Id, x.r.Reference, x.r.Status.ToString(), x.r.StudentName, x.r.ContactPhoneE164, x.r.GuardianName, x.r.GuardianPhoneE164,
            x.r.GradeId, x.Grade, x.r.StudyTrackId, x.Track, x.r.SubjectId, x.Subject, x.r.ServiceTypeId, x.Service,
            x.r.ScopeText, x.r.PreferredDays, x.r.PreferredTimes, x.r.Mode.ToString(), x.Tier, x.r.Notes, x.r.AdminNotes,
            x.r.FulfilledBySessionId, x.r.CreatedAtUtc, x.r.GroupSize)).ToList();
    }

    public async Task UpdateStatusAsync(Guid id, UpdateRequestStatus req, string actor, CancellationToken ct)
    {
        var r = AdminGuard.Found(await db.SessionRequests.FirstOrDefaultAsync(x => x.Id == id, ct), Fields.Request);
        if (!Enum.TryParse<SessionRequestStatus>(req.Status, out var status)) throw new DomainException(AdminErrors.Invalid, "حالة غير معروفة.", "Unknown status.");
        if (req.FulfilledBySessionId is { } sid && !await db.Sessions.AnyAsync(s => s.Id == sid, ct))
            throw new DomainException(AdminErrors.Invalid, "الجلسة مش موجودة.", "Session not found.");
        r.Status = status;
        r.AdminNotes = AdminGuard.Optional(req.AdminNotes, 1000);
        if (req.FulfilledBySessionId is not null) r.FulfilledBySessionId = req.FulfilledBySessionId;
        r.UpdatedAtUtc = clock.UtcNow;
        db.Audit(clock, actor, nameof(SessionRequest), id, "UpdateStatus", req);
        await db.SaveChangesAsync(ct);
    }
}
