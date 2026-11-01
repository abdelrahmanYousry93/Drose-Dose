using Dose.Domain.Common;
using Dose.Domain.Sessions;

namespace Dose.Domain.Pricing;

public sealed record PricingContext(
    Guid ServiceTypeId,
    SessionMode Mode,
    Guid PricingBandId,
    Guid StudyTrackId,
    Guid SubjectId,
    DateTime AtUtc);

public static class PricingErrors
{
    public const string NotConfigured = "pricing.not_configured";
    public const string Ambiguous = "pricing.ambiguous";
    public const string InvalidTiers = "pricing.invalid_tiers";
    public const string TierNotFound = "pricing.tier_not_found";
}

/// <summary>Deterministic plan selection: all dimensions must match; most specific wins; ties are errors.</summary>
public static class PricingPlanResolver
{
    public static PricingPlan Resolve(IEnumerable<PricingPlan> plans, PricingContext ctx)
    {
        var matches = plans.Where(p => Matches(p, ctx)).ToList();
        if (matches.Count == 0)
            throw new DomainException(PricingErrors.NotConfigured, "مفيش خطة تسعير مفعّلة مناسبة للجلسة دي.", "No active pricing plan matches this grade band, track, subject, service type and mode.");

        var best = matches.Max(p => p.Specificity);
        var top = matches.Where(p => p.Specificity == best).ToList();
        if (top.Count > 1)
            throw new DomainException(PricingErrors.Ambiguous,
                $"فيه {top.Count} خطط تسعير بنفس الدرجة بتنطبق: {string.Join("، ", top.Select(p => p.Name))}.",
                $"{top.Count} pricing plans match with equal specificity: {string.Join(", ", top.Select(p => p.Name))}.");
        return top[0];
    }

    public static bool Matches(PricingPlan p, PricingContext ctx) =>
        p.IsActive
        && p.ServiceTypeId == ctx.ServiceTypeId
        && p.Mode == ctx.Mode
        && (p.PricingBandId is null || p.PricingBandId == ctx.PricingBandId)
        && (p.StudyTrackId is null || p.StudyTrackId == ctx.StudyTrackId)
        && (p.SubjectId is null || p.SubjectId == ctx.SubjectId)
        && p.EffectiveFromUtc <= ctx.AtUtc
        && (p.EffectiveToUtc is null || ctx.AtUtc < p.EffectiveToUtc)
        && p.CurrentVersion is not null;

    /// <summary>Two plans conflict if they could both win for the same context.</summary>
    public static bool ConflictsWith(PricingPlan a, PricingPlan b)
    {
        if (a.Id == b.Id || !a.IsActive || !b.IsActive) return false;
        if (a.ServiceTypeId != b.ServiceTypeId || a.Mode != b.Mode) return false;
        if (a.PricingBandId != b.PricingBandId || a.StudyTrackId != b.StudyTrackId || a.SubjectId != b.SubjectId) return false;
        var aEnd = a.EffectiveToUtc ?? DateTime.MaxValue;
        var bEnd = b.EffectiveToUtc ?? DateTime.MaxValue;
        return a.EffectiveFromUtc < bEnd && b.EffectiveFromUtc < aEnd;
    }
}

public static class TierRules
{
    /// <summary>Returns validation errors (empty when valid): ranges, overlaps and gaps.</summary>
    public static IReadOnlyList<LocalizedText> Validate(IEnumerable<PricingTier> tiers)
    {
        var errors = new List<LocalizedText>();
        var ordered = tiers.Where(t => t.IsActive).OrderBy(t => t.MinParticipants).ToList();
        foreach (var t in ordered)
        {
            if (t.MinParticipants < 1) errors.Add(new($"الشريحة '{t.NameAr}': أقل عدد لازم يكون 1 على الأقل.", $"Size '{t.NameAr}': the minimum must be at least 1."));
            if (t.MaxParticipants < t.MinParticipants) errors.Add(new($"الشريحة '{t.NameAr}': أكبر عدد أقل من أقل عدد.", $"Size '{t.NameAr}': the maximum is below the minimum."));
            if (t.PricePerStudent <= 0) errors.Add(new($"الشريحة '{t.NameAr}': السعر لازم يكون أكبر من صفر.", $"Size '{t.NameAr}': the price must be greater than zero."));
        }
        for (var i = 1; i < ordered.Count; i++)
        {
            var prev = ordered[i - 1];
            var cur = ordered[i];
            if (cur.MinParticipants <= prev.MaxParticipants)
                errors.Add(new($"الشريحتين '{prev.NameAr}' و'{cur.NameAr}' متداخلين.", $"Sizes '{prev.NameAr}' and '{cur.NameAr}' overlap."));
            else if (cur.MinParticipants > prev.MaxParticipants + 1)
                errors.Add(new($"فيه فجوة بين '{prev.NameAr}' و'{cur.NameAr}' (من {prev.MaxParticipants + 1} لـ {cur.MinParticipants - 1} طلاب).", $"Gap between '{prev.NameAr}' and '{cur.NameAr}' ({prev.MaxParticipants + 1}–{cur.MinParticipants - 1} students)."));
        }
        return errors;
    }

    public static void EnsureValid(IEnumerable<PricingTier> tiers)
    {
        var errors = Validate(tiers);
        if (errors.Count > 0) throw new DomainException(PricingErrors.InvalidTiers, string.Join(" ", errors.Select(e => e.Ar)), string.Join(" ", errors.Select(e => e.En)));
    }
}

/// <summary>Group price arithmetic for the target-tier-with-top-up model (spec 6.4).</summary>
public static class GroupPricing
{
    /// <summary>
    /// Per-student price if the group ends with <paramref name="count"/> confirmed students.
    /// Below the smallest tier the private price applies when configured, otherwise the smallest tier's price.
    /// </summary>
    public static decimal PriceForCount(PricingPlanVersion version, int count)
    {
        var tiers = version.Tiers.Where(t => t.IsActive).OrderBy(t => t.MinParticipants).ToList();
        if (tiers.Count == 0)
            throw new DomainException(PricingErrors.InvalidTiers, "التسعير الجماعي محتاج شريحة واحدة على الأقل.", "Group pricing needs at least one group size.");

        var match = tiers.FirstOrDefault(t => count >= t.MinParticipants && count <= t.MaxParticipants);
        if (match is not null) return match.PricePerStudent;
        if (count < tiers[0].MinParticipants) return version.PrivatePrice ?? tiers[0].PricePerStudent;
        return tiers[^1].PricePerStudent;
    }

    /// <summary>Highest price a student of this target tier could be asked for (disclosed before booking).</summary>
    public static decimal WorstCasePrice(PricingPlanVersion version, PricingTier target)
    {
        var worst = target.PricePerStudent;
        for (var n = 1; n <= target.MaxParticipants; n++)
            worst = Math.Max(worst, PriceForCount(version, n));
        return worst;
    }

    /// <summary>Extra amount due per student if the group closed at <paramref name="confirmedCount"/>.</summary>
    public static decimal TopUpPerStudent(PricingPlanVersion version, decimal paidPrice, int confirmedCount) =>
        Math.Max(0, PriceForCount(version, Math.Max(1, confirmedCount)) - paidPrice);
}
