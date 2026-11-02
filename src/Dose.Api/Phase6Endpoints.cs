using System.Security.Claims;
using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Application.Teachers;
using Dose.Contracts;
using Dose.Contracts.Admin;

namespace Dose.Api;

/// <summary>Promo codes, private sessions/groups booked by students (with invites), and teacher-proposed sessions.</summary>
public static class Phase6Endpoints
{
    private static string Actor(ClaimsPrincipal u) => u.Identity!.Name!;
    private static string UserId(ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public static void MapPhase6Endpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireRateLimiting(Endpoints.BookingRateLimit);

        api.MapPost("/b/{token}/promo", (string token, PromoPreviewRequest req, BookingService s, CancellationToken ct) =>
            s.PreviewPromoAsync(token, req, ct));

        api.MapGet("/private/slots", (Guid gradeId, Guid subjectId, Guid studyTrackId, Guid serviceTypeId, int size,
                PrivateBookingService s, CancellationToken ct) => s.SlotsAsync(gradeId, subjectId, studyTrackId, serviceTypeId, size, ct));
        api.MapPost("/private/bookings", (CreatePrivateBookingRequest req, PrivateBookingService s, CancellationToken ct) => s.CreateAsync(req, ct));

        api.MapGet("/invites/{code}", (string code, PrivateBookingService s, CancellationToken ct) => s.InviteInfoAsync(code, ct));
        api.MapPost("/invites/{code}/join", (string code, JoinGroupRequest req, PrivateBookingService s, CancellationToken ct) => s.JoinAsync(code, req, ct));

        var admin = app.MapGroup("/api/admin").RequireAuthorization(Endpoints.AdminPolicy);
        admin.MapGet("/promo-codes", (PromoService s, CancellationToken ct) => s.ListAsync(ct));
        admin.MapPost("/promo-codes", async (PromoCodeAdminDto dto, PromoService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveAsync(dto, Actor(u), ct) }));
        admin.MapPost("/session-requests/{id:guid}/offer", (Guid id, OfferRequest req, PrivateBookingService s, ClaimsPrincipal u, CancellationToken ct) =>
            s.OfferAsync(id, req, Actor(u), ct));

        var teacher = app.MapGroup("/api/teacher").RequireAuthorization(Endpoints.TeacherPolicy);
        teacher.MapGet("/proposals", async (TeacherAreaService t, AdminSessionService s, ClaimsPrincipal u, CancellationToken ct) =>
            await s.ProposalsAsync((await t.TeacherForUserAsync(UserId(u), ct)).Id, ct));
        teacher.MapPost("/proposals", async (TeacherProposalRequest req, TeacherAreaService t, AdminSessionService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.ProposeAsync((await t.TeacherForUserAsync(UserId(u), ct)).Id, req, Actor(u), ct) }));
        teacher.MapGet("/pricing-preview", (Guid serviceTypeId, Guid gradeId, Guid studyTrackId, Guid subjectId, AdminSessionService s, CancellationToken ct) =>
            s.PreviewPricingAsync(new PricingPreviewRequest(serviceTypeId, gradeId, studyTrackId, subjectId, "Group"), ct));
    }
}
