using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Persistence;

/// <summary>
/// Owner's decision (17 Nov 2026): open group sessions made by the team are a fixed price per student — everyone books
/// and pays their own price, no group-size tiers, no possible difference. The six launch sessions move to a flat
/// 120 EGP; the Saturday "transition elements" session is withdrawn because the free chapter-1 review on Friday covers
/// it. Only sessions with no bookings at all are touched. Runs once (audit marker).
/// </summary>
public static class LaunchSessionsFixedPrice
{
    const string Marker = "LaunchSessionsFixedPrice/2026-11-17";
    const decimal Price = 120m;

    public static async Task EnsureAsync(DoseDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        if (await db.AuditLogs.AnyAsync(a => a.Entity == "Campaign" && a.EntityId == Marker, ct)) return;
        if (!await db.AuditLogs.AnyAsync(a => a.Entity == "LaunchCatalog" && a.EntityId == "LaunchCatalog/2026-11", ct)) return;

        var launchTitles = new[] { "التيار الكهربي وقانون أوم", "العناصر الانتقالية", "العلاقات والدوال", "الحركة في اتجاه واحد", "قانونا كيرشوف", "حساب المثلثات" };
        var sessions = await db.Sessions.Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Plan)
            .Where(s => s.Mode == SessionMode.Group && launchTitles.Contains(s.ScopeTitleAr) && s.Status == SessionStatus.Published
                        && s.StartUtc >= new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc) && s.StartUtc < new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc))
            .ToListAsync(ct);
        var booked = await db.Bookings.Where(b => sessions.Select(s => s.Id).Contains(b.SessionId)).Select(b => b.SessionId).Distinct().ToListAsync(ct);
        sessions = sessions.Where(s => !booked.Contains(s.Id)).ToList();
        if (sessions.Count == 0) { Mark(db, nowUtc, 0, 0); await db.SaveChangesAsync(ct); return; }

        var basePlan = sessions[0].PricingPlanVersion!.Plan!;
        var plan = new PricingPlan
        {
            Name = "جلسات جماعية — سعر ثابت 120", ServiceTypeId = basePlan.ServiceTypeId, Mode = SessionMode.Group,
            Policy = PricingPolicy.NewBookingsOnly, EffectiveFromUtc = nowUtc, IsActive = false,
        };
        var version = new PricingPlanVersion { PlanId = plan.Id, VersionNo = 1, IsLocked = true };
        var tier = new PricingTier { VersionId = version.Id, NameAr = "سعر ثابت", NameEn = "Fixed price", MinParticipants = 1, MaxParticipants = 60, PricePerStudent = Price, SortOrder = 1 };
        version.Tiers.Add(tier);
        plan.Versions.Add(version);
        db.PricingPlans.Add(plan);

        int moved = 0, withdrawn = 0;
        foreach (var s in sessions)
        {
            if (s.ScopeTitleAr == "العناصر الانتقالية")
            {
                s.Status = SessionStatus.Cancelled; // the free Friday review covers chapter 1
                withdrawn++;
                continue;
            }
            s.PricingPlanVersionId = version.Id;
            s.TargetTierId = tier.Id;
            s.Policy = PricingPolicy.NewBookingsOnly;
            s.MinParticipants = 1;
            moved++;
        }
        Mark(db, nowUtc, moved, withdrawn);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Owner (17 Nov): the fixed-price launch groups take up to 50 students. Only raises seats, never lowers.</summary>
    public static async Task WidenAsync(DoseDbContext db, CancellationToken ct = default)
    {
        var sessions = await db.Sessions
            .Where(s => s.Mode == SessionMode.Group && s.Status == SessionStatus.Published && s.Capacity < 50
                        && s.TargetTier != null && s.TargetTier.NameAr == "سعر ثابت" && s.TargetTier.PricePerStudent == Price)
            .ToListAsync(ct);
        foreach (var s in sessions) s.Capacity = 50;
        if (sessions.Count > 0) await db.SaveChangesAsync(ct);
    }

    static void Mark(DoseDbContext db, DateTime nowUtc, int moved, int withdrawn) =>
        db.AuditLogs.Add(new AuditLog { Actor = "campaign", Entity = "Campaign", EntityId = Marker, Action = $"fixed-price:{moved} withdrawn:{withdrawn}", AtUtc = nowUtc });
}
