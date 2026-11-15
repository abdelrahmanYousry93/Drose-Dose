using Dose.Domain.Administration;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Persistence;

/// <summary>
/// One-time launch set-up requested by the owner (16 Nov 2026): the subjects Dose opens with, one teacher per subject
/// (named by subject until the owner enters the real names in the admin panel), their free time 17:00–20:00 Cairo
/// every day for 30 days from 18 Nov, and a few open group sessions for lessons students commonly get stuck on.
/// Runs once (marker row in the audit log); afterwards everything is edited from the admin panel and never re-added.
/// The sample teacher is unpublished so it is no longer offered to students.
/// </summary>
public static class LaunchCatalog
{
    const string Marker = "LaunchCatalog/2026-11";
    const string SampleTeacher = "مدرس تجريبي (بيانات تجريبية)";

    public static async Task EnsureAsync(DoseDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        if (await db.AuditLogs.AnyAsync(a => a.Entity == "LaunchCatalog" && a.EntityId == Marker, ct)) return;

        var prep = await db.EducationalStages.FirstOrDefaultAsync(s => s.NameAr == "إعدادي", ct);
        var secondary = await db.EducationalStages.FirstOrDefaultAsync(s => s.NameAr == "ثانوي", ct);
        var oneLesson = await db.ServiceTypes.FirstOrDefaultAsync(s => s.Code == "one_lesson", ct);
        var track = await db.StudyTracks.OrderBy(t => t.SortOrder).FirstOrDefaultAsync(ct);
        if (prep is null || secondary is null || oneLesson is null || track is null) return; // catalog not set up here

        var grades = await db.Grades.Where(g => g.StageId == prep.Id || g.StageId == secondary.Id).ToListAsync(ct);
        var prepGrades = grades.Where(g => g.StageId == prep.Id).ToList();
        var secGrades = grades.Where(g => g.StageId == secondary.Id).ToList();
        var links = await db.GradeSubjects.ToListAsync(ct);

        async Task<Subject> SubjectAsync(string ar, string en, int sort, params List<Grade>[] stages)
        {
            var s = await db.Subjects.FirstOrDefaultAsync(x => x.NameAr == ar, ct);
            if (s is null) db.Subjects.Add(s = new Subject { NameAr = ar, NameEn = en, SortOrder = sort });
            foreach (var g in stages.SelectMany(x => x))
                if (!links.Any(l => l.GradeId == g.Id && l.SubjectId == s.Id))
                {
                    var link = new GradeSubject { GradeId = g.Id, SubjectId = s.Id };
                    db.GradeSubjects.Add(link);
                    links.Add(link);
                }
            return s;
        }

        var arabic = await SubjectAsync("لغة عربية", "Arabic", 5, prepGrades, secGrades);
        var math = await SubjectAsync("رياضيات", "Mathematics", 1, prepGrades, secGrades);
        var english = await SubjectAsync("لغة إنجليزية", "English", 4, prepGrades, secGrades);
        var social = await SubjectAsync("دراسات اجتماعية", "Social studies", 6, prepGrades, secGrades);
        var science = await SubjectAsync("علوم", "Science", 2, prepGrades);
        var physics = await SubjectAsync("فيزياء", "Physics", 3, secGrades);
        var chemistry = await SubjectAsync("كيمياء", "Chemistry", 7, secGrades);

        // ----- One teacher per subject; the subject's grades decide whether it is prep, secondary or both -----
        Teacher TeacherFor(Subject s, string en)
        {
            var t = new Teacher { DisplayNameAr = $"مدرس Dose — {s.NameAr}", DisplayNameEn = $"Dose teacher — {en}", IsPublished = true };
            db.Teachers.Add(t);
            db.TeacherSubjects.Add(new TeacherSubject { TeacherId = t.Id, SubjectId = s.Id });
            return t;
        }
        var teachers = new Dictionary<Guid, Teacher>
        {
            [arabic.Id] = TeacherFor(arabic, "Arabic"), [math.Id] = TeacherFor(math, "Mathematics"),
            [english.Id] = TeacherFor(english, "English"), [social.Id] = TeacherFor(social, "Social studies"),
            [science.Id] = TeacherFor(science, "Science"), [physics.Id] = TeacherFor(physics, "Physics"),
            [chemistry.Id] = TeacherFor(chemistry, "Chemistry"),
        };

        // ----- Free time: every day 17:00–20:00 Cairo, 18 Nov → 17 Dec 2026 -----
        var firstDay = new DateTime(2026, 11, 18);
        for (var d = 0; d < 30; d++)
        {
            var start = CairoToUtc(firstDay.AddDays(d).AddHours(17));
            foreach (var t in teachers.Values)
                db.TeacherAvailabilities.Add(new TeacherAvailability
                {
                    TeacherId = t.Id, StartUtc = start, EndUtc = start.AddHours(3), Note = "مواعيد الإطلاق",
                    Status = AvailabilityStatus.Approved, ReviewedBy = "launch", ReviewedAtUtc = nowUtc, CreatedAtUtc = nowUtc,
                });
        }

        // ----- The sample teacher is no longer offered -----
        foreach (var t in await db.Teachers.Where(t => t.DisplayNameAr == SampleTeacher).ToListAsync(ct)) t.IsPublished = false;

        // ----- Open group sessions for commonly requested lessons (small group, current group price list) -----
        var plans = await db.PricingPlans.Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .Where(p => p.IsActive && p.Mode == SessionMode.Group && p.ServiceTypeId == oneLesson.Id
                        && p.PricingBandId == null && p.StudyTrackId == null && p.SubjectId == null
                        && p.EffectiveFromUtc <= nowUtc && (p.EffectiveToUtc == null || p.EffectiveToUtc > nowUtc))
            .ToListAsync(ct);
        var version = plans.Select(p => p.CurrentVersion).FirstOrDefault(v => v is not null && v.Tiers.Count > 0);
        var tier = version?.Tiers.OrderBy(t => t.MinParticipants).First();
        var prep3 = prepGrades.OrderBy(g => g.SortOrder).LastOrDefault();
        var sec3 = secGrades.OrderBy(g => g.SortOrder).LastOrDefault();
        if (version is not null && tier is not null && prep3 is not null && sec3 is not null)
        {
            version.IsLocked = true;
            void Group(int day, Grade grade, Subject subject, string ar, string en)
            {
                var start = CairoToUtc(new DateTime(2026, 11, day, 19, 0, 0));
                db.Sessions.Add(new Session
                {
                    ScopeTitleAr = ar, ScopeTitleEn = en,
                    GradeId = grade.Id, StudyTrackId = track.Id, SubjectId = subject.Id, ServiceTypeId = oneLesson.Id,
                    TeacherId = teachers[subject.Id].Id,
                    StartUtc = start, EndUtc = start.AddMinutes(60), Mode = SessionMode.Group,
                    Capacity = tier.MaxParticipants, MinParticipants = tier.MinParticipants,
                    PricingPlanVersionId = version.Id, TargetTierId = tier.Id, Policy = PricingPolicy.TargetTierWithTopUp,
                    BookingDeadlineUtc = start.AddHours(-24), TierDecisionAtUtc = start.AddHours(-24),
                    Status = SessionStatus.Published,
                });
            }
            Group(20, sec3, physics, "التيار الكهربي وقانون أوم", "Electric current and Ohm's law");
            Group(21, sec3, chemistry, "العناصر الانتقالية", "Transition elements");
            Group(22, prep3, math, "العلاقات والدوال", "Relations and functions");
            Group(23, prep3, science, "الحركة في اتجاه واحد", "Motion in one direction");
            Group(24, sec3, physics, "قانونا كيرشوف", "Kirchhoff's laws");
            Group(25, prep3, math, "حساب المثلثات", "Trigonometry");
        }

        db.AuditLogs.Add(new AuditLog { Actor = "launch", Entity = "LaunchCatalog", EntityId = Marker, Action = "seeded", AtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
    }

    static DateTime CairoToUtc(DateTime local)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(local.AddHours(-2), DateTimeKind.Utc); // Cairo winter time
        }
    }
}
