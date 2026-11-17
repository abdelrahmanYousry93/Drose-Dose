using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class CampaignSeedTests(DatabaseFixture fx)
{
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
            Assert.All(s.PricingPlanVersion!.Tiers, t => Assert.Equal(100m, t.PricePerStudent));
            var code = await db.PromoCodes.SingleAsync(p => p.Code == ChemReviewNov2026.Code);
            Assert.Equal((100m, 20, (Guid?)s.Id), (code.Value, code.MaxTotalUses!.Value, code.SessionId));
        }
    }
}
