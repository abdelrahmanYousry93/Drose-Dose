using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Persistence;

/// <summary>
/// Owner's plan (23 Nov 2026): programming sessions run as numbered rounds («دفعات») of 20 seats only, so a seat feels
/// like a chance; when one fills, the next opens. Rounds 1 and 2 are free (a 100% code per round, 20 uses), round 3 is
/// 50 EGP. Round 1: Saturday 28 Nov, 4 pm Cairo, 90 min, in the Dose room. Runs once (audit marker).
/// </summary>
public static class ProgCohortsDec2026
{
    const string Marker = "ProgCohort/1";
    public static readonly Guid Round1Id = Guid.Parse("01a1f3c0-c4e3-7a20-8000-000000002801");
    public const string Round1Code = "DOFA1";

    public static async Task EnsureAsync(DoseDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        if (await db.AuditLogs.AnyAsync(a => a.Entity == "Campaign" && a.EntityId == Marker, ct)) return;
        var prog = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ProgReviewNov2026.SessionId, ct);
        if (prog is null) return;
        var start = CairoToUtc(new DateTime(2026, 11, 28, 16, 0, 0));
        if (nowUtc >= start) return;
        var session = new Session
        {
            ScopeTitleAr = "الدفعة 1 — تطبيقات الويب: الموقع بيشتغل إزاي؟ (Client / Server / HTTP)",
            ScopeTitleEn = "Round 1 — Web applications: how a website works (Client / Server / HTTP)",
            GradeId = prog.GradeId, StudyTrackId = prog.StudyTrackId, SubjectId = prog.SubjectId, ServiceTypeId = prog.ServiceTypeId, TeacherId = prog.TeacherId,
            StartUtc = start, EndUtc = start.AddMinutes(90), Mode = SessionMode.Group, Capacity = 20, MinParticipants = 1,
            PricingPlanVersionId = prog.PricingPlanVersionId, TargetTierId = prog.TargetTierId, Policy = PricingPolicy.NewBookingsOnly,
            BookingDeadlineUtc = start.AddHours(-3), TierDecisionAtUtc = start.AddHours(-3),
            Status = SessionStatus.Published, MeetingUrl = DoseRoom.Marker, MeetingProvider = DoseRoom.Provider,
        };
        db.Sessions.Add(session);
        db.Entry(session).Property(s => s.Id).CurrentValue = Round1Id;
        if (!await db.PromoCodes.AnyAsync(p => p.Code == Round1Code, ct))
            db.PromoCodes.Add(new PromoCode
            {
                Code = Round1Code, Description = "دفعة البرمجة 1 — ببلاش (20 مقعد)", Kind = PromoKind.Percent, Value = 100,
                SessionId = Round1Id, MaxTotalUses = 20, MaxUsesPerPhone = 1, ValidToUtc = start, CreatedBy = "campaign", CreatedAtUtc = nowUtc,
            });
        db.AuditLogs.Add(new AuditLog { Actor = "campaign", Entity = "Campaign", EntityId = Marker, Action = "seeded", AtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Owner's change (24 Nov): 10 seats only, free for those 10, and no "round" wording (people don't get it).
    /// Applied once, only while no more than 10 have booked.
    /// </summary>
    public static async Task TenSeatsAsync(DoseDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        const string marker = "ProgCohort/1/ten-seats";
        if (await db.AuditLogs.AnyAsync(a => a.Entity == "Campaign" && a.EntityId == marker, ct)) return;
        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == Round1Id, ct);
        if (s is null) return;
        BookingStatus[] dead = [BookingStatus.Cancelled, BookingStatus.Expired, BookingStatus.Refunded];
        if (await db.Bookings.CountAsync(b => b.SessionId == Round1Id && !dead.Contains(b.Status), ct) > 10) return;
        s.Capacity = 10;
        s.ScopeTitleAr = "تطبيقات الويب: الموقع بيشتغل إزاي؟ (Client / Server / HTTP)";
        s.ScopeTitleEn = "Web applications: how a website works (Client / Server / HTTP)";
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.Code == Round1Code, ct);
        if (promo is not null) { promo.MaxTotalUses = 10; promo.Description = "حصة البرمجة 28 نوفمبر — ببلاش لأول 10"; }
        db.AuditLogs.Add(new AuditLog { Actor = "owner", Entity = "Campaign", EntityId = marker, Action = "10 seats", AtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
    }

    static DateTime CairoToUtc(DateTime local)
    {
        try { return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")); }
        catch (TimeZoneNotFoundException) { return DateTime.SpecifyKind(local.AddHours(-2), DateTimeKind.Utc); }
    }
}
