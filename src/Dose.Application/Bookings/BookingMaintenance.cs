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
    public Task<int> ExpireStaleAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        return db.Bookings
            .Where(b => (b.Status == BookingStatus.PendingHold && b.HoldExpiresAtUtc <= now) ||
                        (b.Status == BookingStatus.PendingPayment && b.PaymentDueAtUtc <= now))
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.Status, BookingStatus.Expired), ct);
    }
}
