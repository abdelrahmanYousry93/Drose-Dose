using Dose.Domain.Common;
using Dose.Domain.Sessions;

namespace Dose.Domain.Pricing;

public enum PricingPolicy
{
    TargetTierWithTopUp = 0,
    NewBookingsOnly = 1,
    RepriceAllBookings = 2,
}

/// <summary>
/// Matching dimensions for a price. Null dimensions match anything; the most specific
/// matching plan wins (see <see cref="PricingPlanResolver"/>).
/// </summary>
public sealed class PricingPlan : Entity
{
    public required string Name { get; set; }
    public Guid ServiceTypeId { get; set; }
    public SessionMode Mode { get; set; }
    public Guid? PricingBandId { get; set; }
    public Guid? StudyTrackId { get; set; }
    public Guid? SubjectId { get; set; }
    public string Currency { get; set; } = "EGP";
    public PricingPolicy Policy { get; set; } = PricingPolicy.TargetTierWithTopUp;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public List<PricingPlanVersion> Versions { get; set; } = [];

    public int Specificity =>
        (PricingBandId is null ? 0 : 1) + (StudyTrackId is null ? 0 : 1) + (SubjectId is null ? 0 : 1);

    public PricingPlanVersion? CurrentVersion =>
        Versions.Where(v => v.IsActive).OrderByDescending(v => v.VersionNo).FirstOrDefault();
}

/// <summary>Price values. Locked (immutable) once a session or booking references it.</summary>
public sealed class PricingPlanVersion : Entity
{
    public Guid PlanId { get; set; }
    public PricingPlan? Plan { get; set; }
    public int VersionNo { get; set; }
    /// <summary>Price for one student alone (private session, or a group that shrinks to one).</summary>
    public decimal? PrivatePrice { get; set; }
    public bool IsLocked { get; set; }
    public bool IsActive { get; set; } = true;
    public List<PricingTier> Tiers { get; set; } = [];
}

/// <summary>Group size option: participant range and per-student price.</summary>
public sealed class PricingTier : Entity
{
    public Guid VersionId { get; set; }
    public required string NameAr { get; set; }
    public int MinParticipants { get; set; }
    public int MaxParticipants { get; set; }
    public decimal PricePerStudent { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Immutable price record for a confirmed booking. Insert-only.</summary>
public sealed class PriceSnapshot : Entity
{
    public Guid PlanId { get; init; }
    public Guid PlanVersionId { get; init; }
    public required string Currency { get; init; }
    public decimal BasePrice { get; init; }
    public Guid? TargetTierId { get; init; }
    public int? TierMin { get; init; }
    public int? TierMax { get; init; }
    public decimal? WorstCasePrice { get; init; }
    public decimal FinalAmount { get; init; }
    public PricingPolicy Policy { get; init; }
    public DateTime CalculatedAtUtc { get; init; }
}
