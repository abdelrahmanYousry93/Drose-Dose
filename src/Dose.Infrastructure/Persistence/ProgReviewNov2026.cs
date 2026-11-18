using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Persistence;

/// <summary>
/// Owner's request (18 Nov 2026): a session for the new 2nd-secondary baccalaureate subject «البرمجة والذكاء الاصطناعي»
/// (engineering &amp; computer science track) on Saturday 21 Nov at 4 pm Cairo, taught by the owner himself (shown under his own
/// name, generic avatar). 50 EGP per student, free for the first 15 who use code CODE15 (100%, one use per number).
/// Topic: web applications — the part of the term-1 book taught in November (overall structure + how they communicate).
/// Fixed session id so the direct booking link can go on the poster before deploy. Runs once (audit marker).
/// </summary>
public static class ProgReviewNov2026
{
    const string Marker = "ProgSession/2026-11-21";
    public static readonly Guid SessionId = Guid.Parse("01a1f3c0-c4e3-7a20-8000-000000002021");
    public const string Code = "CODE15";
    public const string SubjectAr = "البرمجة والذكاء الاصطناعي";
    public const string TeacherAr = "عبدالرحمن كامل";
    const string ScopeAr = "تطبيقات الويب: الموقع بيشتغل إزاي؟ (Client / Server / HTTP)";
    const string ScopeEn = "Web applications: how a website works (Client / Server / HTTP)";

    /// <summary>Owner's correction (18 Nov): show him as «عبدالرحمن كامل» (no title). Renames the first deploy's teacher once.</summary>
    public static async Task RenameTeacherAsync(DoseDbContext db, CancellationToken ct = default)
    {
        var t = await db.Teachers.FirstOrDefaultAsync(x => x.DisplayNameAr == "عبدالرحمن يسري" && x.PhotoUrl == "/m/teachers/prog-avatar.svg", ct);
        if (t is null) return;
        t.DisplayNameAr = TeacherAr;
        t.DisplayNameEn = "Abdelrahman Kamel";
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Owner's request (18 Nov): one Google Meet room for both campaign sessions (chemistry Fri 6 pm, programming Sat 4 pm).
    /// Fills it only where no link was set yet, so a later change from the admin page is kept.
    /// </summary>
    public static async Task SetMeetingLinksAsync(DoseDbContext db, CancellationToken ct = default)
    {
        const string Url = "https://meet.google.com/qrn-siof-kxa";
        Guid[] ids = [ChemReviewNov2026.SessionId, SessionId];
        var sessions = await db.Sessions.Where(s => ids.Contains(s.Id) && s.MeetingUrl == null).ToListAsync(ct);
        foreach (var s in sessions) { s.MeetingUrl = Url; s.MeetingProvider = "GoogleMeet"; }
        if (sessions.Count > 0) await db.SaveChangesAsync(ct);
    }

    public static async Task EnsureAsync(DoseDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        if (await db.AuditLogs.AnyAsync(a => a.Entity == "Campaign" && a.EntityId == Marker, ct)) return;

        var secondary = await db.EducationalStages.FirstOrDefaultAsync(s => s.NameAr == "ثانوي", ct);
        var service = await db.ServiceTypes.FirstOrDefaultAsync(s => s.Code == "one_lesson", ct);
        var track = await db.StudyTracks.OrderBy(t => t.SortOrder).FirstOrDefaultAsync(ct);
        if (secondary is null || service is null || track is null) return; // catalog not set up here
        var secGrades = await db.Grades.Where(g => g.StageId == secondary.Id).OrderBy(g => g.SortOrder).ToListAsync(ct);
        if (secGrades.Count < 2) return;
        var sec2 = secGrades.FirstOrDefault(g => g.NameAr == "الصف الثاني الثانوي") ?? secGrades[1];

        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.NameAr == SubjectAr, ct);
        if (subject is null) db.Subjects.Add(subject = new Subject { NameAr = SubjectAr, NameEn = "Programming & AI", SortOrder = 8 });
        if (!await db.GradeSubjects.AnyAsync(l => l.GradeId == sec2.Id && l.SubjectId == subject.Id, ct))
            db.GradeSubjects.Add(new GradeSubject { GradeId = sec2.Id, SubjectId = subject.Id });

        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.DisplayNameAr == TeacherAr, ct);
        if (teacher is null)
            db.Teachers.Add(teacher = new Teacher
            {
                DisplayNameAr = TeacherAr, DisplayNameEn = "Abdelrahman Kamel", PhotoUrl = "/m/teachers/prog-avatar.svg",
                BioAr = "مطوّر برمجيات بيشتغل في بناء المواقع والتطبيقات، وبيشرح البرمجة بأمثلة من الشغل الحقيقي.",
                BioEn = "Software developer who builds websites and apps, and teaches programming with real-world examples.",
                IsPublished = true,
            });
        if (!await db.TeacherSubjects.AnyAsync(x => x.TeacherId == teacher.Id && x.SubjectId == subject.Id, ct))
            db.TeacherSubjects.Add(new TeacherSubject { TeacherId = teacher.Id, SubjectId = subject.Id });

        // Flat price: one tier, 50 EGP whatever the group size. Inactive plan, so only this session uses it.
        var plan = new PricingPlan
        {
            Name = "برمجة 2ث — سعر ثابت 50", ServiceTypeId = service.Id, Mode = SessionMode.Group,
            SubjectId = subject.Id, Policy = PricingPolicy.NewBookingsOnly, EffectiveFromUtc = nowUtc, IsActive = false,
        };
        var version = new PricingPlanVersion { PlanId = plan.Id, VersionNo = 1, IsLocked = true };
        var tier = new PricingTier { VersionId = version.Id, NameAr = "سعر ثابت", NameEn = "Flat price", MinParticipants = 1, MaxParticipants = 50, PricePerStudent = 50, SortOrder = 1 };
        version.Tiers.Add(tier);
        plan.Versions.Add(version);
        db.PricingPlans.Add(plan);

        var start = CairoToUtc(new DateTime(2026, 11, 21, 16, 0, 0));
        var session = new Session
        {
            ScopeTitleAr = ScopeAr, ScopeTitleEn = ScopeEn,
            GradeId = sec2.Id, StudyTrackId = track.Id, SubjectId = subject.Id, ServiceTypeId = service.Id, TeacherId = teacher.Id,
            StartUtc = start, EndUtc = start.AddMinutes(90), Mode = SessionMode.Group,
            Capacity = 50, MinParticipants = 1,
            PricingPlanVersionId = version.Id, TargetTierId = tier.Id, Policy = PricingPolicy.NewBookingsOnly,
            BookingDeadlineUtc = start.AddMinutes(-30), TierDecisionAtUtc = start.AddMinutes(-30),
            Status = SessionStatus.Published,
        };
        db.Sessions.Add(session);
        db.Entry(session).Property(s => s.Id).CurrentValue = SessionId;

        if (!await db.PromoCodes.AnyAsync(p => p.Code == Code, ct))
            db.PromoCodes.Add(new PromoCode
            {
                Code = Code, Description = "حصة برمجة 2ث 21 نوفمبر — مجانًا لأول 15", Kind = PromoKind.Percent, Value = 100,
                SessionId = SessionId, MaxTotalUses = 15, MaxUsesPerPhone = 1, ValidToUtc = start,
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
