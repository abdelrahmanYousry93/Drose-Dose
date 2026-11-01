using System.Security.Claims;
using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Application.Sessions;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;

namespace Dose.Api;

/// <summary>Phase 3: session requests, group top-up choices, student cancellation, admin follow-ups.</summary>
public static class Phase3Endpoints
{
    public static void MapPhase3Endpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ---------- Public ----------
        api.MapGet("/price-options", async (Guid serviceTypeId, Guid gradeId, Guid studyTrackId, Guid subjectId,
            SessionRequestService s, CancellationToken ct) =>
            await s.PriceOptionsAsync(serviceTypeId, gradeId, studyTrackId, subjectId, ct) is { } o ? Results.Ok(o) : Results.NoContent());
        api.MapPost("/session-requests", (CreateSessionRequest req, SessionRequestService s, CancellationToken ct) => s.CreateAsync(req, ct))
            .RequireRateLimiting(Endpoints.BookingRateLimit);

        var b = api.MapGroup("/b/{token}").RequireRateLimiting(Endpoints.BookingRateLimit);
        b.MapPost("/top-up-choice", async (string token, TopUpChoiceRequest req, BookingService s, CancellationToken ct) =>
        {
            await s.ChooseTopUpAsync(token, req.Choice, ct);
            return Results.NoContent();
        });
        b.MapPost("/top-up-proofs", async (string token, HttpRequest request, BookingService s, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("screenshot");
            if (file is null || !Guid.TryParse(form["paymentMethodId"], out var methodId))
                return Results.Problem(title: "Screenshot and payment method are required.", statusCode: 400,
                    extensions: new Dictionary<string, object?> { ["code"] = BookingErrors.InvalidFile });
            await using var stream = file.OpenReadStream();
            await s.SubmitTopUpProofAsync(token, methodId, stream, file.Length, form["senderReference"], ct);
            return Results.NoContent();
        }).DisableAntiforgery();
        b.MapPost("/cancel", async (string token, CancelBookingRequest req, BookingService s, CancellationToken ct) =>
        {
            await s.CancelAsync(token, req.AcceptNoRefund, ct);
            return Results.NoContent();
        });

        // ---------- Admin ----------
        var admin = api.MapGroup("/admin").RequireAuthorization(Endpoints.AdminPolicy);
        static string Actor(ClaimsPrincipal u) => u.Identity!.Name!;

        admin.MapGet("/follow-ups", (FollowUpService s, CancellationToken ct) => s.ListAsync(ct));
        admin.MapPost("/follow-ups/{bookingId:guid}/waive", async (Guid bookingId, ResolveRequest r, FollowUpService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.WaiveAsync(bookingId, r.Note, Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapPost("/follow-ups/{bookingId:guid}/refunded", async (Guid bookingId, ResolveRequest r, FollowUpService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.MarkRefundedAsync(bookingId, r.Note, Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapPost("/follow-ups/{bookingId:guid}/transfer-done", async (Guid bookingId, ResolveRequest r, FollowUpService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.MarkTransferDoneAsync(bookingId, r.Note, Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapPost("/sessions/{id:guid}/decide", async (Guid id, GroupDecisionService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.DecideAsync(id, Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapGet("/session-requests", (string? status, SessionRequestService s, CancellationToken ct) => s.ListAsync(status, ct));
        admin.MapPost("/session-requests/{id:guid}/status", async (Guid id, UpdateRequestStatus req, SessionRequestService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.UpdateStatusAsync(id, req, Actor(u), ct);
            return Results.NoContent();
        });
    }
}
