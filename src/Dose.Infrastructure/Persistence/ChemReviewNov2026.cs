using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Persistence;

/// <summary>
/// Owner's request (17 Nov 2026): a chemistry review for 3rd secondary — chapter 1 (transition elements) — on Friday 20 Nov at 6 pm
/// Cairo, 100 EGP per student, free for the first 20 who use code CHEM20 (100%, one use per number, this session only).
/// The session has a fixed id so its direct booking link (/sessions/{id}) could go on the poster before deploy.
/// Runs once (audit marker); afterwards the session and the code are edited from the admin panel.
/// </summary>
public static class ChemReviewNov2026
{
    const string Marker = "ChemReview/2026-11-20";
    public static readonly Guid SessionId = Guid.Parse("01a1f3c0-c4e3-7a20-8000-000000002011");
    public const string Code = "CHEM20";
    // Owner narrowed the review to chapter 1 only (17 Nov, 12:41).
    const string ScopeAr = "مراجعة الباب الأول: العناصر الانتقالية (السلسلة الانتقالية الأولى + الحديد)";
    const string ScopeEn = "Review of chapter 1: transition elements (first transition series + iron)";
    const string OldScopeAr = "مراجعة الباب الأول والتاني: العناصر الانتقالية والتحليل الكيميائي (الكشف عن الشقوق + المعايرة)";

    /// <summary>If an earlier deploy already created the session with chapters 1+2, narrow it to chapter 1.</summary>
    public static async Task NarrowToChapterOneAsync(DoseDbContext db, CancellationToken ct = default)
    {
        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == SessionId && x.ScopeTitleAr == OldScopeAr, ct);
        if (s is null) return;
        s.ScopeTitleAr = ScopeAr;
        s.ScopeTitleEn = ScopeEn;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Owner's request (17 Nov): the chemistry teacher shows as «محمد علاء» with a generic avatar (no real photo).</summary>
    public static async Task NameChemistryTeacherAsync(DoseDbContext db, CancellationToken ct = default)
    {
        var t = await db.Teachers.FirstOrDefaultAsync(x => x.DisplayNameAr == "مدرس Dose — كيمياء", ct);
        if (t is null) return; // already renamed (or edited in the admin panel): never overwrite
        t.DisplayNameAr = "محمد علاء";
        t.DisplayNameEn = "Mohamed Alaa";
        if (t.PhotoFileKey is null && t.PhotoUrl is null) t.PhotoUrl = "/m/teachers/chem-avatar.svg";
        await db.SaveChangesAsync(ct);
    }

    public static async Task EnsureAsync(DoseDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        if (await db.AuditLogs.AnyAsync(a => a.Entity == "Campaign" && a.EntityId == Marker, ct)) return;

        var chemistry = await db.Subjects.FirstOrDefaultAsync(s => s.NameAr == "كيمياء", ct);
        var secondary = await db.EducationalStages.FirstOrDefaultAsync(s => s.NameAr == "ثانوي", ct);
        var service = await db.ServiceTypes.FirstOrDefaultAsync(s => s.Code == "general_revision", ct)
                      ?? await db.ServiceTypes.FirstOrDefaultAsync(s => s.Code == "one_lesson", ct);
        var track = await db.StudyTracks.OrderBy(t => t.SortOrder).FirstOrDefaultAsync(ct);
        if (chemistry is null || secondary is null || service is null || track is null) return; // catalog not set up here
        var sec3 = await db.Grades.Where(g => g.StageId == secondary.Id).OrderByDescending(g => g.SortOrder).FirstOrDefaultAsync(ct);
        var teacher = await (from ts in db.TeacherSubjects
                             join t in db.Teachers on ts.TeacherId equals t.Id
                             where ts.SubjectId == chemistry.Id && t.IsPublished
                             select t).FirstOrDefaultAsync(ct);
        if (sec3 is null || teacher is null) return;

        // Flat price: one tier, 100 EGP whatever the group size, so no group difference is ever owed. Inactive plan, so
        // it is never picked automatically for other sessions — only this session points at it.
        var plan = new PricingPlan
        {
            Name = "مراجعة كيمياء 3ث — سعر ثابت 100", ServiceTypeId = service.Id, Mode = SessionMode.Group,
            SubjectId = chemistry.Id, Policy = PricingPolicy.NewBookingsOnly, EffectiveFromUtc = nowUtc, IsActive = false,
        };
        var version = new PricingPlanVersion { PlanId = plan.Id, VersionNo = 1, IsLocked = true };
        var tier = new PricingTier { VersionId = version.Id, NameAr = "سعر ثابت", NameEn = "Flat price", MinParticipants = 1, MaxParticipants = 60, PricePerStudent = 100, SortOrder = 1 };
        version.Tiers.Add(tier);
        plan.Versions.Add(version);
        db.PricingPlans.Add(plan);

        var start = CairoToUtc(new DateTime(2026, 11, 20, 18, 0, 0));
        var session = new Session
        {
            ScopeTitleAr = ScopeAr,
            ScopeTitleEn = ScopeEn,
            GradeId = sec3.Id, StudyTrackId = track.Id, SubjectId = chemistry.Id, ServiceTypeId = service.Id, TeacherId = teacher.Id,
            StartUtc = start, EndUtc = start.AddMinutes(120), Mode = SessionMode.Group,
            Capacity = 60, MinParticipants = 1,
            PricingPlanVersionId = version.Id, TargetTierId = tier.Id, Policy = PricingPolicy.NewBookingsOnly,
            BookingDeadlineUtc = start.AddMinutes(-30), TierDecisionAtUtc = start.AddMinutes(-30),
            Status = SessionStatus.Published,
        };
        db.Sessions.Add(session);
        db.Entry(session).Property(s => s.Id).CurrentValue = SessionId;

        if (!await db.PromoCodes.AnyAsync(p => p.Code == Code, ct))
            db.PromoCodes.Add(new PromoCode
            {
                Code = Code, Description = "مراجعة كيمياء الباب الأول 20 نوفمبر — مجانًا لأول 20", Kind = PromoKind.Percent, Value = 100,
                SessionId = SessionId, MaxTotalUses = 20, MaxUsesPerPhone = 1, ValidToUtc = start,
                CreatedBy = "campaign", CreatedAtUtc = nowUtc,
            });

        db.AuditLogs.Add(new AuditLog { Actor = "campaign", Entity = "Campaign", EntityId = Marker, Action = "seeded", AtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
    }

    static DateTime CairoToUtc(DateTime local)
    {
        try { return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")); }
        catch (TimeZoneNotFoundException) { return DateTime.SpecifyKind(local.AddHours(-2), DateTimeKind.Utc); }
    }
}
