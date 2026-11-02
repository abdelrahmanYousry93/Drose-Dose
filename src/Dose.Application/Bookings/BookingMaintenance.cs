using Dose.Application.Abstractions;
using Dose.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Bookings;

/// <summary>
/// Background clean-up: marks expired seat holds and unpaid bookings as Expired. Booking operations already ignore
/// expired rows inline, so correctness never depends on this job — it keeps data and admin views tidy.
/// </summary>
public sealed class BookingMaintenance(IDoseDbContext db, IClock clock)
{
    public async Task<int> ExpireStaleAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var expired = await db.Bookings
            .Where(b => (b.Status == BookingStatus.PendingHold && b.HoldExpiresAtUtc <= now) ||
                        (b.Status == BookingStatus.PendingPayment && b.PaymentDueAtUtc <= now))
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.Status, BookingStatus.Expired), ct);

        // A private session/group nobody holds any more (the organizer didn't pay in time) frees the teacher's time.
        var live = db.Bookings.Where(Sessions.SeatRules.OccupiesSeat(now));
        await db.Sessions
            .Where(s => s.IsClosed && s.Status == Domain.Sessions.SessionStatus.Published && !live.Any(b => b.SessionId == s.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, Domain.Sessions.SessionStatus.Cancelled), ct);
        return expired;
    }
}
