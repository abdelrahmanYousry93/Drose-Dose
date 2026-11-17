using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class CampaignSeedTests(DatabaseFixture fx)
{
    [Fact]
    public async Task Programming_session_seed_creates_subject_teacher_session_and_free_code_once()
    {
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");
        await using (var db = fx.NewDb())
        {
            await ProgReviewNov2026.EnsureAsync(db, fx.SeedTime);
            await ProgReviewNov2026.EnsureAsync(db, fx.SeedTime); // second run is a no-op
        }
        await using (var db = fx.NewDb())
        {
            var s = await db.Sessions.Include(x => x.PricingPlanVersion!).ThenInclude(v => v.Tiers).SingleAsync(x => x.Id == ProgReviewNov2026.SessionId);
            Assert.Equal(new DateTime(2026, 11, 21, 14, 0, 0, DateTimeKind.Utc), s.StartUtc); // 4 pm Cairo
            Assert.All(s.PricingPlanVersion!.Tiers, t => Assert.Equal(50m, t.PricePerStudent));
            var subject = await db.Subjects.SingleAsync(x => x.NameAr == ProgReviewNov2026.SubjectAr);
            Assert.Equal(subject.Id, s.SubjectId);
            Assert.True(await db.GradeSubjects.AnyAsync(l => l.SubjectId == subject.Id && l.GradeId == s.GradeId));
            var teacher = await db.Teachers.SingleAsync(x => x.DisplayNameAr == ProgReviewNov2026.TeacherAr);
            Assert.Equal((teacher.Id, true), (s.TeacherId, teacher.IsPublished));
            var code = await db.PromoCodes.SingleAsync(p => p.Code == ProgReviewNov2026.Code);
            Assert.Equal((100m, 15, (Guid?)s.Id), (code.Value, code.MaxTotalUses!.Value, code.SessionId));
            Assert.Equal(1, await db.Sessions.CountAsync(x => x.Id == ProgReviewNov2026.SessionId));
        }
    }

    [Fact]
    public async Task Chemistry_review_seed_creates_the_session_at_its_fixed_id_with_a_flat_price_and_the_free_code_once()
    {
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");
        await using (var db = fx.NewDb())
        {
            // Chemistry with a published teacher, as production has (without running the launch catalog, which would
            // change the shared test catalog for other tests).
            var chem = await db.Subjects.FirstOrDefaultAsync(x => x.NameAr == "كيمياء");
            if (chem is null) db.Subjects.Add(chem = new Dose.Domain.Catalog.Subject { NameAr = "كيمياء", NameEn = "Chemistry", SortOrder = 7 });
            var t = new Dose.Domain.Teachers.Teacher { DisplayNameAr = "مدرس كيمياء اختبار", IsPublished = true };
            db.Teachers.Add(t);
            db.TeacherSubjects.Add(new Dose.Domain.Teachers.TeacherSubject { TeacherId = t.Id, SubjectId = chem.Id });
            await db.SaveChangesAsync();
            await ChemReviewNov2026.EnsureAsync(db, fx.SeedTime);
            await ChemReviewNov2026.EnsureAsync(db, fx.SeedTime); // second run is a no-op
        }
        await using (var db = fx.NewDb())
        {
            var s = await db.Sessions.Include(x => x.PricingPlanVersion!).ThenInclude(v => v.Tiers).SingleAsync(x => x.Id == ChemReviewNov2026.SessionId);
            Assert.Equal(new DateTime(2026, 11, 20, 16, 0, 0, DateTimeKind.Utc), s.StartUtc); // 6 pm Cairo
            Assert.Equal("مراجعة شاملة على الباب الأول كله: العناصر الانتقالية", s.ScopeTitleAr);
            Assert.All(s.PricingPlanVersion!.Tiers, t => Assert.Equal(100m, t.PricePerStudent));
            var code = await db.PromoCodes.SingleAsync(p => p.Code == ChemReviewNov2026.Code);
            Assert.Equal((100m, 20, (Guid?)s.Id), (code.Value, code.MaxTotalUses!.Value, code.SessionId));
        }
        // The session page shows the free offer with the places left.
        await using (var app = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(b, "Production");
            b.UseSetting("ConnectionStrings:Dose", fx.ConnectionString);
            b.UseSetting("Jobs:Enabled", "false");
            b.UseSetting("Seed:LaunchCatalog", "false");
        }))
        {
            var res = await app.CreateClient().GetAsync($"/api/sessions/{ChemReviewNov2026.SessionId}/offer");
            Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
            var o = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<System.Text.Json.JsonElement>(res.Content);
            Assert.Equal("CHEM20", o.GetProperty("code").GetString());
            Assert.Equal(20, o.GetProperty("remaining").GetInt32());
        }
        // Fixed price: no "how the group price works" terms, and no group-difference box to tick.
        var clock = new FakeClock(fx.SeedTime);
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            var hold = await svc.CreateHoldAsync(ChemReviewNov2026.SessionId, default);
            Assert.Null((await svc.GetViewAsync(hold.AccessToken, default)).GroupTerms);
            await svc.SubmitDetailsAsync(hold.AccessToken, new Dose.Contracts.SubmitDetailsRequest("طالب كيمياء", "01222000111", "ولي أمر", "01222000112",
                AcceptTerms: true, AcceptTopUpTerms: false, ExpectedPrice: 100m), default);
            Assert.Equal("PendingPayment", (await svc.GetViewAsync(hold.AccessToken, default)).Status.ToString());
        }
    }
}
