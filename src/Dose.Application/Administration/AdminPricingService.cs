using Dose.Application.Abstractions;
using Dose.Contracts.Admin;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Administration;

/// <summary>
/// Pricing plans (matching dimensions) and their price versions. A version referenced by any session or
/// confirmed booking is locked; changing prices then creates a new version, so existing sessions and
/// bookings keep the prices they were published/sold with (spec 6.5).
/// </summary>
public sealed class AdminPricingService(IDoseDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<PricingPlanAdminDto>> PlansAsync(CancellationToken ct)
    {
        var plans = await db.PricingPlans.AsNoTracking().Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .OrderBy(p => p.ServiceTypeId).ThenBy(p => p.Mode).ThenBy(p => p.Name).AsSplitQuery().ToListAsync(ct);
        return plans.Select(ToDto).ToList();
    }

    public async Task<PricingPlanAdminDto> PlanAsync(Guid id, CancellationToken ct)
    {
        var plan = AdminGuard.Found(await db.PricingPlans.AsNoTracking().Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .AsSplitQuery().FirstOrDefaultAsync(p => p.Id == id, ct), Fields.PricingPlan);
        return ToDto(plan);
    }

    public async Task<Guid> SavePlanAsync(PricingPlanAdminDto dto, string actor, CancellationToken ct)
    {
        if (!Enum.TryParse<SessionMode>(dto.Mode, out var mode)) throw new DomainException(AdminErrors.Invalid, "اختار نوع الجلسة.", "Choose the session type.");
        if (!Enum.TryParse<PricingPolicy>(dto.Policy, out var policy)) throw new DomainException(AdminErrors.Invalid, "اختار سياسة التسعير.", "Choose the pricing policy.");
        var currency = (dto.Currency ?? "").Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)) throw new DomainException(AdminErrors.Invalid, "العملة لازم تكون 3 حروف زي EGP.", "Currency must be a 3-letter code like EGP.");
        if (!await db.ServiceTypes.AnyAsync(s => s.Id == dto.ServiceTypeId, ct)) throw new DomainException(AdminErrors.Invalid, "اختار نوع الخدمة.", "Choose the service type.");
        if (dto.PricingBandId is { } b && !await db.PricingBands.AnyAsync(x => x.Id == b, ct)) throw new DomainException(AdminErrors.Invalid, "شريحة التسعير مش موجودة.", "Pricing band not found.");
        if (dto.StudyTrackId is { } t && !await db.StudyTracks.AnyAsync(x => x.Id == t, ct)) throw new DomainException(AdminErrors.Invalid, "المنهج مش موجود.", "Curriculum track not found.");
        if (dto.SubjectId is { } s && !await db.Subjects.AnyAsync(x => x.Id == s, ct)) throw new DomainException(AdminErrors.Invalid, "المادة مش موجودة.", "Subject not found.");
        var from = DateTime.SpecifyKind(dto.EffectiveFromUtc, DateTimeKind.Utc);
        DateTime? to = dto.EffectiveToUtc is { } x2 ? DateTime.SpecifyKind(x2, DateTimeKind.Utc) : null;
        if (to <= from) throw new DomainException(AdminErrors.Invalid, "تاريخ النهاية لازم يكون بعد تاريخ البداية.", "The end date must be after the start date.");

        var plan = dto.Id is { } id
            ? AdminGuard.Found(await db.PricingPlans.FindAsync([id], ct), Fields.PricingPlan)
            : new PricingPlan { Name = "" };
        plan.Name = AdminGuard.Name(dto.Name, Fields.PlanName, 150);
        plan.ServiceTypeId = dto.ServiceTypeId;
        plan.Mode = mode;
        plan.PricingBandId = dto.PricingBandId;
        plan.StudyTrackId = dto.StudyTrackId;
        plan.SubjectId = dto.SubjectId;
        plan.Currency = currency;
        plan.Policy = policy;
        plan.EffectiveFromUtc = from;
        plan.EffectiveToUtc = to;
        plan.IsActive = dto.IsActive;

        var others = await db.PricingPlans.AsNoTracking()
            .Where(p => p.Id != plan.Id && p.IsActive && p.ServiceTypeId == plan.ServiceTypeId && p.Mode == plan.Mode)
            .ToListAsync(ct);
        var clash = others.FirstOrDefault(o => PricingPlanResolver.ConflictsWith(plan, o));
        if (clash is not null)
            throw new DomainException(AdminErrors.Conflict, $"فيه خطة تانية \"{clash.Name}\" بنفس الأبعاد وفي نفس الفترة. عدّل الأبعاد أو التواريخ أو وقّف واحدة منهم.", $"Another plan \"{clash.Name}\" has the same dimensions in the same period. Change the dimensions or dates, or deactivate one of them.");

        if (dto.Id is null) db.PricingPlans.Add(plan);
        db.Audit(clock, actor, nameof(PricingPlan), plan.Id, dto.Id is null ? "Create" : "Update",
            new { dto.Name, dto.Mode, dto.PricingBandId, dto.StudyTrackId, dto.SubjectId, dto.Policy, from, to, dto.IsActive });
        await db.SaveChangesAsync(ct);
        return plan.Id;
    }

    /// <summary>Saves prices: edits the current version if it's unused, otherwise creates a new version.</summary>
    public async Task<PricingPlanAdminDto> SavePricesAsync(Guid planId, SavePricesRequest req, string actor, CancellationToken ct)
    {
        var plan = AdminGuard.Found(await db.PricingPlans.Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .AsSplitQuery().FirstOrDefaultAsync(p => p.Id == planId, ct), Fields.PricingPlan);

        if (req.PrivatePrice is <= 0) throw new DomainException(AdminErrors.Invalid, "سعر الطالب الواحد لازم يكون أكبر من صفر.", "The single-student price must be greater than zero.");
        var tiers = (req.Tiers ?? []).Select((t, i) => new PricingTier
        {
            NameAr = AdminGuard.Name(t.NameAr, Fields.TierName),
            NameEn = AdminGuard.Optional(t.NameEn, 100),
            MinParticipants = t.MinParticipants, MaxParticipants = t.MaxParticipants,
            PricePerStudent = t.PricePerStudent, SortOrder = t.SortOrder == 0 ? i + 1 : t.SortOrder,
        }).ToList();

        if (plan.Mode == SessionMode.Private)
        {
            if (req.PrivatePrice is null) throw new DomainException(AdminErrors.Invalid, "الجلسة الفردية محتاجة سعر.", "A private session needs a price.");
            if (tiers.Count > 0) throw new DomainException(AdminErrors.Invalid, "الجلسة الفردية مالهاش شرائح جروب.", "A private session has no group sizes.");
        }
        else
        {
            if (tiers.Count == 0) throw new DomainException(AdminErrors.Invalid, "الجلسة الجماعية محتاجة شريحة واحدة على الأقل.", "A group session needs at least one group size.");
            var errors = TierRules.Validate(tiers);
            if (errors.Count > 0) throw new DomainException(PricingErrors.InvalidTiers, string.Join(" ", errors.Select(e => e.Ar)), string.Join(" ", errors.Select(e => e.En)));
        }

        var current = plan.CurrentVersion;
        PricingPlanVersion target;
        if (current is { IsLocked: false })
        {
            target = current;
            db.PricingTiers.RemoveRange(target.Tiers);
            target.Tiers.Clear();
        }
        else
        {
            target = new PricingPlanVersion { PlanId = plan.Id, VersionNo = (plan.Versions.Count == 0 ? 0 : plan.Versions.Max(v => v.VersionNo)) + 1 };
            db.PricingPlanVersions.Add(target);
        }
        target.PrivatePrice = req.PrivatePrice;
        foreach (var t in tiers)
        {
            t.VersionId = target.Id;
            db.PricingTiers.Add(t);
        }

        db.Audit(clock, actor, nameof(PricingPlanVersion), target.Id, current is { IsLocked: false } ? "UpdatePrices" : "NewVersion",
            new { planId, target.VersionNo, req.PrivatePrice, tiers = tiers.Select(t => new { t.NameAr, t.MinParticipants, t.MaxParticipants, t.PricePerStudent }) });
        await db.SaveChangesAsync(ct);
        return await PlanAsync(planId, ct);
    }

    private static PricingPlanAdminDto ToDto(PricingPlan p)
    {
        var v = p.CurrentVersion;
        return new PricingPlanAdminDto(p.Id, p.Name, p.ServiceTypeId, p.Mode.ToString(), p.PricingBandId, p.StudyTrackId, p.SubjectId,
            p.Currency, p.Policy.ToString(), p.EffectiveFromUtc, p.EffectiveToUtc, p.IsActive,
            v is null ? null : new PricingVersionAdminDto(v.Id, v.VersionNo, v.PrivatePrice, v.IsLocked,
                v.Tiers.Where(t => t.IsActive).OrderBy(t => t.MinParticipants)
                    .Select(t => new TierAdminDto(t.Id, t.NameAr, t.MinParticipants, t.MaxParticipants, t.PricePerStudent, t.SortOrder, t.NameEn)).ToList()));
    }
}
