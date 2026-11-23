using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Persistence;

/// <summary>
/// Owner's request (23 Nov 2026): a test session in the Dose room to try the whole flow as student and teacher.
/// Programming, 2nd secondary, «عبدالرحمن كامل», today 13:25 UTC (4:25 pm Riyadh / 3:25 pm Cairo) for an hour, 50 EGP with
/// code TESTROOM (100%). Not listed for long: only created if the start is still ahead. Runs once (audit marker).
/// </summary>
public static class TestRoomNov2026
{
    const string Marker = "TestRoom/2026-11-23";
    public static readonly Guid SessionId = Guid.Parse("01a1f3c0-c4e3-7a20-8000-000000002301");
    public const string Code = "TESTROOM";

    public static async Task EnsureAsync(DoseDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        var start = new DateTime(2026, 11, 23, 13, 25, 0, DateTimeKind.Utc);
        if (nowUtc >= start) return;
        if (await db.AuditLogs.AnyAsync(a => a.Entity == "Campaign" && a.EntityId == Marker, ct)) return;
        var prog = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ProgReviewNov2026.SessionId, ct);
        if (prog is null) return;
        var session = new Session
        {
            ScopeTitleAr = "جلسة تجربة غرفة دروس Dose", ScopeTitleEn = "Dose room test session",
            GradeId = prog.GradeId, StudyTrackId = prog.StudyTrackId, SubjectId = prog.SubjectId, ServiceTypeId = prog.ServiceTypeId, TeacherId = prog.TeacherId,
            StartUtc = start, EndUtc = start.AddMinutes(60), Mode = SessionMode.Group, Capacity = 5, MinParticipants = 1,
            PricingPlanVersionId = prog.PricingPlanVersionId, TargetTierId = prog.TargetTierId, Policy = PricingPolicy.NewBookingsOnly,
            BookingDeadlineUtc = start.AddMinutes(30), TierDecisionAtUtc = start.AddMinutes(-5),
            Status = SessionStatus.Published, MeetingUrl = DoseRoom.Marker, MeetingProvider = DoseRoom.Provider,
        };
        db.Sessions.Add(session);
        db.Entry(session).Property(s => s.Id).CurrentValue = SessionId;
        if (!await db.PromoCodes.AnyAsync(p => p.Code == Code, ct))
            db.PromoCodes.Add(new PromoCode
            {
                Code = Code, Description = "تجربة غرفة دروس Dose", Kind = PromoKind.Percent, Value = 100,
                SessionId = SessionId, MaxTotalUses = 5, MaxUsesPerPhone = 2, ValidToUtc = start.AddMinutes(60),
                CreatedBy = "owner", CreatedAtUtc = nowUtc,
            });
        db.AuditLogs.Add(new AuditLog { Actor = "owner", Entity = "Campaign", EntityId = Marker, Action = "seeded", AtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
    }
}
