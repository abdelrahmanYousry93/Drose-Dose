using Dose.Domain.Sessions;
using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class LaunchCatalogTests(DatabaseFixture fx)
{
    [Fact]
    public async Task Launch_catalog_adds_teachers_free_time_and_group_sessions_once()
    {
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");
        await using var db = fx.NewDb();
        // Everything here is rolled back so the shared sample database stays as other tests expect it.
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = new DateTime(2026, 11, 15, 21, 0, 0, DateTimeKind.Utc);

        await LaunchCatalog.EnsureAsync(db, now);
        await LaunchCatalog.EnsureAsync(db, now); // second run adds nothing

        var teachers = await db.Teachers.Where(t => t.DisplayNameAr.StartsWith("مدرس Dose — ")).ToListAsync();
        Assert.Equal(7, teachers.Count);
        Assert.All(teachers, t => Assert.True(t.IsPublished));
        Assert.False((await db.Teachers.SingleAsync(t => t.DisplayNameAr == "مدرس تجريبي (بيانات تجريبية)")).IsPublished);

        // 17:00–20:00 Cairo (15:00–18:00 UTC in winter) every day for 30 days from 18 Nov, per teacher.
        var ids = teachers.Select(t => t.Id).ToList();
        var windows = await db.TeacherAvailabilities.Where(a => ids.Contains(a.TeacherId)).ToListAsync();
        Assert.Equal(7 * 30, windows.Count);
        Assert.Equal(new DateTime(2026, 11, 18, 15, 0, 0), windows.Min(w => w.StartUtc));
        Assert.Equal(new DateTime(2026, 12, 17, 18, 0, 0), windows.Max(w => w.EndUtc));

        // Subjects by stage: chemistry and physics secondary only, science prep only, Arabic/social studies in both.
        async Task<List<string>> SubjectsOf(string gradeName) => await (
            from gs in db.GradeSubjects join g in db.Grades on gs.GradeId equals g.Id join s in db.Subjects on gs.SubjectId equals s.Id
            where g.NameAr == gradeName orderby s.NameAr select s.NameAr).ToListAsync();
        var prep3 = await SubjectsOf("الصف الثالث الإعدادي");
        var sec3 = await SubjectsOf("الصف الثالث الثانوي");
        Assert.Equal(["دراسات اجتماعية", "رياضيات", "علوم", "لغة إنجليزية", "لغة عربية"], prep3);
        Assert.Equal(["دراسات اجتماعية", "رياضيات", "فيزياء", "كيمياء", "لغة إنجليزية", "لغة عربية"], sec3);
        Assert.DoesNotContain("لغة عربية", await SubjectsOf("الصف الأول الابتدائي"));

        // Students can book a chemistry teacher's free time from 18 Nov.
        var clock = new FakeClock(now);
        var grade = await db.Grades.SingleAsync(g => g.NameAr == "الصف الثالث الثانوي");
        var chemistry = await db.Subjects.SingleAsync(s => s.NameAr == "كيمياء");
        var service = await db.ServiceTypes.SingleAsync(s => s.Code == "one_lesson");
        var track = await db.StudyTracks.OrderBy(t => t.SortOrder).FirstAsync();
        var slots = await fx.Private(db, clock).SlotsAsync(grade.Id, chemistry.Id, track.Id, service.Id, 1, default);
        Assert.NotEmpty(slots.Slots);
        Assert.All(slots.Slots, s => Assert.Equal("مدرس Dose — كيمياء", s.TeacherName));
        Assert.Equal(new DateTime(2026, 11, 18, 15, 0, 0), slots.Slots.Min(s => s.StartUtc));

        // Six published open group sessions, bookable until 24 hours before each.
        var sessions = await db.Sessions.Where(s => ids.Contains(s.TeacherId)).ToListAsync();
        Assert.Equal(6, sessions.Count);
        Assert.All(sessions, s =>
        {
            Assert.Equal(SessionStatus.Published, s.Status);
            Assert.Equal(SessionMode.Group, s.Mode);
            Assert.False(s.IsClosed);
            Assert.True(s.IsOpenForBooking(now));
        });
        // The group session hour is not offered again as a private slot for the same teacher.
        var physics = await db.Subjects.SingleAsync(s => s.NameAr == "فيزياء");
        var physicsSlots = await fx.Private(db, clock).SlotsAsync(grade.Id, physics.Id, track.Id, service.Id, 1, default);
        Assert.DoesNotContain(physicsSlots.Slots, s => s.StartUtc == new DateTime(2026, 11, 20, 17, 0, 0));
        Assert.Contains(physicsSlots.Slots, s => s.StartUtc == new DateTime(2026, 11, 20, 15, 0, 0));

        // 17 Nov: open groups become a fixed 120 per student; the Saturday chapter-1 session is withdrawn.
        await LaunchSessionsFixedPrice.EnsureAsync(db, now);
        await LaunchSessionsFixedPrice.EnsureAsync(db, now); // once
        await LaunchSessionsFixedPrice.WidenAsync(db);
        db.ChangeTracker.Clear();
        var after = await db.Sessions.Include(x => x.PricingPlanVersion!).ThenInclude(v => v.Tiers).Include(x => x.TargetTier)
            .Where(x => ids.Contains(x.TeacherId)).ToListAsync();
        Assert.Equal(SessionStatus.Cancelled, after.Single(x => x.ScopeTitleAr == "العناصر الانتقالية").Status);
        var open = after.Where(x => x.Status == SessionStatus.Published).ToList();
        Assert.Equal(5, open.Count);
        Assert.All(open, x =>
        {
            Assert.Equal(120m, Dose.Application.Sessions.SessionPricing.CurrentPrice(x));
            Assert.Null(Dose.Application.Sessions.SessionPricing.GroupTerms(x));
            Assert.Equal(50, x.Capacity);
        });
    }
}
