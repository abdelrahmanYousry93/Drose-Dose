using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;

namespace Dose.UnitTests;

public class PricingTests
{
    private static PricingTier Tier(string name, int min, int max, decimal price) =>
        new() { NameAr = name, MinParticipants = min, MaxParticipants = max, PricePerStudent = price };

    private static PricingPlanVersion Version(decimal? privatePrice, params PricingTier[] tiers)
    {
        var v = new PricingPlanVersion { VersionNo = 1, PrivatePrice = privatePrice };
        v.Tiers.AddRange(tiers);
        return v;
    }

    // Example tiers from the spec (sample values only).
    private static readonly PricingTier Small = Tier("2–4", 2, 4, 120);
    private static readonly PricingTier Medium = Tier("5–9", 5, 9, 100);
    private static readonly PricingTier Large = Tier("10–15", 10, 15, 80);

    [Theory]
    [InlineData(1, 150)]   // alone → private price
    [InlineData(2, 120)]   // lower boundary of small
    [InlineData(4, 120)]   // upper boundary of small
    [InlineData(5, 100)]   // lower boundary of medium
    [InlineData(9, 100)]
    [InlineData(10, 80)]
    [InlineData(15, 80)]
    public void PriceForCount_uses_matching_tier(int count, decimal expected)
    {
        var v = Version(150, Small, Medium, Large);
        Assert.Equal(expected, GroupPricing.PriceForCount(v, count));
    }

    [Fact]
    public void PriceForCount_without_private_price_falls_back_to_smallest_tier()
    {
        var v = Version(null, Small, Medium);
        Assert.Equal(120, GroupPricing.PriceForCount(v, 1));
    }

    [Fact]
    public void WorstCase_for_medium_target_is_private_price()
    {
        var v = Version(150, Small, Medium, Large);
        Assert.Equal(150, GroupPricing.WorstCasePrice(v, Medium));
    }

    [Fact]
    public void WorstCase_never_below_target_price()
    {
        var v = Version(null, Small, Medium, Large);
        Assert.Equal(120, GroupPricing.WorstCasePrice(v, Medium));
        Assert.Equal(120, GroupPricing.WorstCasePrice(v, Small));
    }

    [Theory]
    [InlineData(7, 0)]     // target reached → nothing extra
    [InlineData(5, 0)]
    [InlineData(3, 20)]    // fell to small tier → 120 - 100
    [InlineData(1, 50)]    // alone → 150 - 100
    [InlineData(0, 50)]    // treated as one student
    public void TopUp_is_difference_to_actual_tier(int confirmed, decimal expected)
    {
        var v = Version(150, Small, Medium, Large);
        Assert.Equal(expected, GroupPricing.TopUpPerStudent(v, Medium.PricePerStudent, confirmed));
    }

    [Fact]
    public void Tiers_valid_when_contiguous()
    {
        Assert.Empty(TierRules.Validate([Small, Medium, Large]));
    }

    [Fact]
    public void Tiers_overlap_is_rejected()
    {
        var errors = TierRules.Validate([Tier("a", 2, 5, 120), Tier("b", 5, 9, 100)]);
        Assert.Contains(errors, e => e.Contains("متداخلين"));
    }

    [Fact]
    public void Tiers_gap_is_rejected()
    {
        var errors = TierRules.Validate([Tier("a", 2, 4, 120), Tier("b", 6, 9, 100)]);
        Assert.Contains(errors, e => e.Contains("فجوة"));
    }

    [Fact]
    public void Tiers_invalid_range_and_price_rejected()
    {
        var errors = TierRules.Validate([Tier("a", 0, 4, 120), Tier("b", 9, 5, 0)]);
        Assert.Contains(errors, e => e.Contains("1 على الأقل"));
        Assert.Contains(errors, e => e.Contains("أقل من أقل عدد"));
        Assert.Contains(errors, e => e.Contains("أكبر من صفر"));
    }

    [Fact]
    public void EnsureValid_throws_domain_error()
    {
        var ex = Assert.Throws<DomainException>(() => TierRules.EnsureValid([Tier("a", 2, 5, 1), Tier("b", 4, 9, 1)]));
        Assert.Equal(PricingErrors.InvalidTiers, ex.Code);
    }

    [Fact]
    public void PriceForCount_with_no_tiers_is_a_configuration_error()
    {
        var ex = Assert.Throws<DomainException>(() => GroupPricing.PriceForCount(Version(150), 3));
        Assert.Equal(PricingErrors.InvalidTiers, ex.Code);
    }
}

public class PlanResolverTests
{
    private static readonly Guid Service = Guid.NewGuid(), Band = Guid.NewGuid(), Track = Guid.NewGuid(), Subject = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private static PricingContext Ctx => new(Service, SessionMode.Group, Band, Track, Subject, Now);

    private static PricingPlan Plan(string name, Guid? band = null, Guid? track = null, Guid? subject = null,
        SessionMode mode = SessionMode.Group, DateTime? from = null, DateTime? to = null)
    {
        var p = new PricingPlan
        {
            Name = name, ServiceTypeId = Service, Mode = mode, PricingBandId = band, StudyTrackId = track, SubjectId = subject,
            EffectiveFromUtc = from ?? Now.AddDays(-1), EffectiveToUtc = to,
        };
        p.Versions.Add(new PricingPlanVersion { PlanId = p.Id, VersionNo = 1, PrivatePrice = 100 });
        return p;
    }

    [Fact]
    public void Most_specific_plan_wins()
    {
        var general = Plan("general");
        var byBand = Plan("band", band: Band);
        var byBandTrack = Plan("band+track", band: Band, track: Track);
        Assert.Same(byBandTrack, PricingPlanResolver.Resolve([general, byBand, byBandTrack], Ctx));
    }

    [Fact]
    public void Non_matching_dimensions_are_ignored()
    {
        var general = Plan("general");
        var otherBand = Plan("other band", band: Guid.NewGuid(), track: Track);
        Assert.Same(general, PricingPlanResolver.Resolve([general, otherBand], Ctx));
    }

    [Fact]
    public void No_match_is_a_configuration_error_not_a_fallback()
    {
        var privateOnly = Plan("private", mode: SessionMode.Private);
        var ex = Assert.Throws<DomainException>(() => PricingPlanResolver.Resolve([privateOnly], Ctx));
        Assert.Equal(PricingErrors.NotConfigured, ex.Code);
    }

    [Fact]
    public void Expired_or_future_plans_do_not_match()
    {
        var expired = Plan("expired", to: Now.AddHours(-1));
        var future = Plan("future", from: Now.AddDays(1));
        Assert.Throws<DomainException>(() => PricingPlanResolver.Resolve([expired, future], Ctx));
    }

    [Fact]
    public void Equal_specificity_is_ambiguous()
    {
        var a = Plan("a", band: Band);
        var b = Plan("b", track: Track);
        var ex = Assert.Throws<DomainException>(() => PricingPlanResolver.Resolve([a, b], Ctx));
        Assert.Equal(PricingErrors.Ambiguous, ex.Code);
    }

    [Fact]
    public void Same_dimensions_with_overlapping_dates_conflict()
    {
        var a = Plan("a", band: Band, to: Now.AddDays(10));
        var b = Plan("b", band: Band, from: Now.AddDays(5));
        var c = Plan("c", band: Band, from: Now.AddDays(10));
        Assert.True(PricingPlanResolver.ConflictsWith(a, b));
        Assert.False(PricingPlanResolver.ConflictsWith(a, c));
    }
}
