using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class AdminTests(DatabaseFixture fx)
{
    private const string Actor = "admin@test";

    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private sealed record Ctx(Guid ServiceTypeId, Guid GradeId, Guid TrackId, Guid SubjectId, Guid TeacherId, Guid BandId);

    /// <summary>Uses the general-revision service type so these plans never affect the booking tests.</summary>
    private async Task<Ctx> ContextAsync()
    {
        await using var db = fx.NewDb();
        var service = await db.ServiceTypes.SingleAsync(s => s.Code == "general_revision");
        var grade = await db.Grades.Include(g => g.Stage).FirstAsync(g => g.Stage!.NameAr == "ثانوي");
        var subjectId = await db.GradeSubjects.Where(gs => gs.GradeId == grade.Id).Select(gs => gs.SubjectId).FirstAsync();
        var track = await db.StudyTracks.FirstAsync();
        var teacher = await db.Teachers.FirstAsync();
        return new Ctx(service.Id, grade.Id, track.Id, subjectId, teacher.Id, grade.PricingBandId);
    }

    private PricingPlanAdminDto Plan(Ctx c, string name, string mode = "Group", Guid? subjectId = null) =>
        new(null, name, c.ServiceTypeId, mode, c.BandId, null, subjectId, "EGP", nameof(PricingPolicy.TargetTierWithTopUp),
            fx.SeedTime.AddDays(-30), null, true, null);

    private static TierAdminDto T(string n, int min, int max, decimal p) => new(null, n, min, max, p, 0);

    private SessionSaveRequest NewSession(Ctx c, Guid? tierId, string mode = "Group", int daysAhead = 6) =>
        new(c.ServiceTypeId, c.GradeId, c.TrackId, c.SubjectId, null, null, "مراجعة الوحدة الأولى", "تفاصيل", c.TeacherId,
            fx.SeedTime.AddDays(daysAhead), 90, mode, tierId, "https://meet.google.com/abc-defg-hij");

    [Fact]
    public async Task Prices_edit_in_place_until_used_then_new_version_keeps_existing_session_prices()
    {
        RequireDb();
        var c = await ContextAsync();
        // Other tests add subject-specific plans for c.SubjectId (more specific, so they would win the resolution).
        // Use another subject of the same grade so this band-level plan is the one that applies, whatever the test order.
        await using (var db0 = fx.NewDb())
            c = c with { SubjectId = await db0.GradeSubjects.Where(gs => gs.GradeId == c.GradeId && gs.SubjectId != c.SubjectId)
                .Select(gs => gs.SubjectId).FirstAsync() };
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var pricing = new AdminPricingService(db, clock);

        var planId = await pricing.SavePlanAsync(Plan(c, "مراجعة — جماعي — ثانوي"), Actor, default);
        var v1 = await pricing.SavePricesAsync(planId, new SavePricesRequest(200, [T("صغير", 2, 4, 150), T("كبير", 5, 10, 120)]), Actor, default);
        Assert.Equal(1, v1.CurrentVersion!.VersionNo);
        Assert.False(v1.CurrentVersion.IsLocked);

        var v1b = await pricing.SavePricesAsync(planId, new SavePricesRequest(200, [T("صغير", 2, 4, 140), T("كبير", 5, 10, 110)]), Actor, default);
        Assert.Equal(1, v1b.CurrentVersion!.VersionNo); // unused version edited in place
        Assert.Equal(140, v1b.CurrentVersion.Tiers[0].PricePerStudent);

        await using var db2 = fx.NewDb();
        var sessions = fx.AdminSessions(db2, clock);
        var preview = await sessions.PreviewPricingAsync(new PricingPreviewRequest(c.ServiceTypeId, c.GradeId, c.TrackId, c.SubjectId, "Group"), default);
        Assert.Equal(planId, preview.PlanId);
        var smallTier = preview.Tiers.Single(t => t.NameAr == "صغير").Id!.Value;
        var sessionId = await sessions.CreateAsync(NewSession(c, smallTier), Actor, default);

        await using var db3 = fx.NewDb();
        var v2 = await new AdminPricingService(db3, clock).SavePricesAsync(planId,
            new SavePricesRequest(220, [T("صغير", 2, 4, 160), T("كبير", 5, 10, 130)]), Actor, default);
        Assert.Equal(2, v2.CurrentVersion!.VersionNo); // v1 was locked by the session

        await using var db4 = fx.NewDb();
        var created = await fx.AdminSessions(db4, clock).GetAsync(sessionId, default);
        Assert.Equal(140, created.PricePerStudent); // session keeps the price it was created with
        Assert.Equal(4, created.Capacity);
        Assert.Equal(2, created.MinParticipants);
        Assert.Equal("Draft", created.Status);
        Assert.Equal(created.StartUtc.AddHours(-24), created.TierDecisionAtUtc);
        Assert.Equal(created.TierDecisionAtUtc, created.BookingDeadlineUtc);
        Assert.True(await db4.AuditLogs.AnyAsync(a => a.EntityId == sessionId.ToString() && a.Action == "Create"));
    }

    [Fact]
    public async Task Conflicting_plan_and_invalid_tiers_are_rejected()
    {
        RequireDb();
        var c = await ContextAsync();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var pricing = new AdminPricingService(db, clock);
        var subjectSpecific = Plan(c, "مراجعة فردية خاصة بالمادة", "Private", c.SubjectId);
        await pricing.SavePlanAsync(subjectSpecific, Actor, default);

        await using var db2 = fx.NewDb();
        var conflict = await Assert.ThrowsAsync<DomainException>(() =>
            new AdminPricingService(db2, clock).SavePlanAsync(subjectSpecific with { Name = "نسخة مكررة" }, Actor, default));
        Assert.Equal(AdminErrors.Conflict, conflict.Code);

        await using var db3 = fx.NewDb();
        var p3 = new AdminPricingService(db3, clock);
        var groupPlan = await p3.SavePlanAsync(Plan(c, "جروب بشرائح غلط", "Group", c.SubjectId), Actor, default);
        var overlap = await Assert.ThrowsAsync<DomainException>(() =>
            p3.SavePricesAsync(groupPlan, new SavePricesRequest(null, [T("أولى", 2, 5, 100), T("تانية", 5, 9, 90)]), Actor, default));
        Assert.Equal(PricingErrors.InvalidTiers, overlap.Code);
        var noTiers = await Assert.ThrowsAsync<DomainException>(() => p3.SavePricesAsync(groupPlan, new SavePricesRequest(100, []), Actor, default));
        Assert.Equal(AdminErrors.Invalid, noTiers.Code);
    }

    [Fact]
    public async Task Session_without_matching_pricing_cannot_be_created()
    {
        RequireDb();
        var c = await ContextAsync();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var otherTrack = await db.StudyTracks.Where(t => t.Id != c.TrackId).Select(t => t.Id).FirstAsync();
        var otherBandGrade = await db.Grades.Where(g => g.PricingBandId != c.BandId)
            .Where(g => db.GradeSubjects.Any(gs => gs.GradeId == g.Id && gs.SubjectId == c.SubjectId)).Select(g => g.Id).FirstAsync();
        var svc = fx.AdminSessions(db, clock);
        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(
            NewSession(c, null, "Private") with { GradeId = otherBandGrade, StudyTrackId = otherTrack }, Actor, default));
        Assert.Equal(PricingErrors.NotConfigured, ex.Code);
    }

    [Fact]
    public async Task Publish_update_with_bookings_and_cancel()
    {
        RequireDb();
        var c = await ContextAsync();
        var clock = new FakeClock(fx.SeedTime);

        Guid sessionId, tierId;
        await using (var db = fx.NewDb())
        {
            var pricing = new AdminPricingService(db, clock);
            var planId = await pricing.SavePlanAsync(Plan(c, "مراجعة جماعي — للاختبار", "Group", c.SubjectId) with
                { StudyTrackId = c.TrackId }, Actor, default);
            var dto = await pricing.SavePricesAsync(planId, new SavePricesRequest(250, [T("صغير", 2, 3, 180)]), Actor, default);
            tierId = dto.CurrentVersion!.Tiers[0].Id!.Value;
            var svc = fx.AdminSessions(db, clock);
            sessionId = await svc.CreateAsync(NewSession(c, tierId, daysAhead: 8), Actor, default);
            await svc.PublishAsync(sessionId, Actor, default);
        }

        string token;
        await using (var db = fx.NewDb())
        {
            var hold = await fx.Bookings(db, clock).CreateHoldAsync(sessionId, default);
            Assert.Equal(180, hold.Price);
            Assert.Equal(250, hold.WorstCasePrice);
            token = hold.AccessToken;
            await fx.Bookings(db, clock).SubmitDetailsAsync(token,
                new SubmitDetailsRequest("طالب", "01091112223", null, null, true, true, 180), default);
        }

        await using (var db = fx.NewDb())
        {
            var svc = fx.AdminSessions(db, clock);
            var blocked = await Assert.ThrowsAsync<DomainException>(() =>
                svc.UpdateAsync(sessionId, NewSession(c, tierId, "Private", 8), Actor, default));
            Assert.Equal(AdminErrors.Conflict, blocked.Code);

            await svc.UpdateAsync(sessionId, NewSession(c, tierId, daysAhead: 9) with { ScopeTitleAr = "ميعاد جديد" }, Actor, default);
            var updated = await svc.GetAsync(sessionId, default);
            Assert.Equal(fx.SeedTime.AddDays(9), updated.StartUtc);
            Assert.Equal("ميعاد جديد", updated.ScopeTitleAr);
            Assert.Equal(1, updated.ActiveBookingCount);
        }

        await using (var db = fx.NewDb())
        {
            var bookings = await fx.AdminSessions(db, clock).BookingsAsync(sessionId, default);
            var reissued = await fx.AdminSessions(db, clock).ReissueLinkAsync(bookings[0].Id, Actor, default);
            Assert.True(AccessTokens.LooksValid(reissued.AccessToken));
            Assert.Equal("+201091112223", reissued.ContactPhone);
            var old = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).GetViewAsync(token, default));
            Assert.Equal(BookingErrors.NotFound, old.Code);
            token = reissued.AccessToken;
        }

        await using (var db = fx.NewDb())
        {
            var refunds = await fx.AdminSessions(db, clock).CancelAsync(sessionId, "المدرس اعتذر", Actor, default);
            Assert.Equal(0, refunds); // the only booking was unpaid → cancelled, nothing to refund
            var view = await fx.Bookings(db, clock).GetViewAsync(token, default);
            Assert.Equal(nameof(BookingStatus.Cancelled), view.Status);
            var hold = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).CreateHoldAsync(sessionId, default));
            Assert.Equal(BookingErrors.SessionClosed, hold.Code);
        }
    }

    [Fact]
    public async Task Catalog_grade_subject_links_and_guardian_rule_are_editable()
    {
        RequireDb();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var svc = fx.Catalog(db, clock);

        var stageId = await svc.SaveStageAsync(new StageAdminDto(null, "مرحلة تجريبية", true, 99, true), Actor, default);
        var band = (await svc.PricingBandsAsync(default))[0];
        var subjectId = await svc.SaveSubjectAsync(new LookupAdminDto(null, "مادة جديدة", 99, true), Actor, default);
        var gradeId = await svc.SaveGradeAsync(new GradeAdminDto(null, "صف تجريبي", stageId, band.Id!.Value, null, 99, true, [subjectId]), Actor, default);

        var grades = await svc.GradesAsync(default);
        Assert.Equal([subjectId], grades.Single(g => g.Id == gradeId).SubjectIds);

        await svc.SaveGradeAsync(new GradeAdminDto(gradeId, "صف تجريبي", stageId, band.Id!.Value, null, 99, true, []), Actor, default);
        Assert.Empty((await svc.GradesAsync(default)).Single(g => g.Id == gradeId).SubjectIds);

        var bad = await Assert.ThrowsAsync<DomainException>(() => svc.SaveStageAsync(new StageAdminDto(null, "x", false, 1, true), Actor, default));
        Assert.Equal(AdminErrors.Invalid, bad.Code);

        var badUrl = await Assert.ThrowsAsync<DomainException>(() =>
            svc.SaveTeacherAsync(new TeacherAdminDto(null, "أ. اختبار", "http://insecure.example/p.jpg", null, null, 3, true), Actor, default));
        Assert.Equal(AdminErrors.Invalid, badUrl.Code);

        await svc.SavePaymentMethodAsync(new PaymentMethodAdminDto(null, "bank_test", "تحويل بنكي", "IBAN-TEST", null, 9, false), Actor, default);
        var dup = await Assert.ThrowsAsync<DomainException>(() =>
            svc.SavePaymentMethodAsync(new PaymentMethodAdminDto(null, "bank_test", "تكرار", "X-123", null, 9, false), Actor, default));
        Assert.Equal(AdminErrors.Conflict, dup.Code);
    }
}
