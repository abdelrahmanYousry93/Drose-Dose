using Dose.Application.Bookings;
using Dose.Domain.Bookings;
using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dose.Api;

public sealed record SessionOffer(string Code, string Kind, decimal Value, int? Total, int? Remaining, DateTime? ValidToUtc);

/// <summary>
/// Public, live offer for one session: an active promo code made for exactly this session (e.g. CHEM20 — free for the
/// first 20), with how many uses are left, so the session page can show it up front. General codes (FB15…) are not
/// listed here: those stay in the ads they belong to.
/// </summary>
public static class SessionOfferEndpoints
{
    public static void MapSessionOfferEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/sessions/{id:guid}/offer", async (Guid id, DoseDbContext db, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var promo = await db.PromoCodes.AsNoTracking()
                .Where(p => p.SessionId == id && p.IsActive && (p.ValidFromUtc == null || p.ValidFromUtc <= now) && (p.ValidToUtc == null || p.ValidToUtc > now))
                .OrderByDescending(p => p.Kind == PromoKind.Percent ? p.Value : 0).FirstOrDefaultAsync(ct);
            if (promo is null) return Results.NoContent();
            int? remaining = null;
            if (promo.MaxTotalUses is { } max)
            {
                var used = await db.Bookings.CountAsync(b => b.PromoCodeId == promo.Id && PromoService.CountingStatuses.Contains(b.Status), ct);
                remaining = Math.Max(0, max - used);
            }
            return Results.Ok(new SessionOffer(promo.Code, promo.Kind.ToString(), promo.Value, promo.MaxTotalUses, remaining, promo.ValidToUtc));
        });
}
