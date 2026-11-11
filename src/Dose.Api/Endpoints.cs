using System.Security.Claims;
using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Application.Catalog;
using Dose.Application.Payments;
using Dose.Application.Sessions;
using Dose.Contracts;
using Dose.Domain.Bookings;
using Dose.Domain.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Dose.Api;

public static class Endpoints
{
    public const string AdminPolicy = "Admin";
    public const string TeacherPolicy = "Teacher";
    public const string BookingRateLimit = "booking";
    public const string LoginRateLimit = "login";

    static async Task<bool> DatabaseUpAsync(Dose.Infrastructure.Persistence.DoseDbContext db, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try { return await db.Database.CanConnectAsync(timeout.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
    }

    public static void MapDoseEndpoints(this WebApplication app)
    {
        // Share page of a scheduled Facebook link post (Open Graph card + redirect to the site).
        app.MapGet("/p/{key}", async (string key, MarketingPublisher publisher, CancellationToken ct) =>
            await publisher.SharePageAsync(key, ct) is { } html
                ? Results.Content(html, "text/html; charset=utf-8")
                : Results.Redirect("/"))
            .DisableRateLimiting();

        var api = app.MapGroup("/api");

        // Platform health check: the web app itself is up (200) even while the database is unreachable, so the host
        // keeps serving the site and a clear "temporarily unavailable" message instead of its own error page.
        // /api/health/db answers 503 when the database is unreachable (for monitoring).
        api.MapGet("/health", async (Dose.Infrastructure.Persistence.DoseDbContext db, CancellationToken ct) =>
            Results.Ok(new { status = "ok", database = await DatabaseUpAsync(db, ct) ? "ok" : "unavailable" }))
            .DisableRateLimiting();
        // What the marketing publisher has queued and published (no secrets) — read by the marketing agency.
        api.MapGet("/marketing/status", async (MarketingPublisher publisher, CancellationToken ct) =>
            Results.Ok(await publisher.StatusAsync(ct)));
        api.MapGet("/health/db", async (Dose.Infrastructure.Persistence.DoseDbContext db, CancellationToken ct) =>
            await DatabaseUpAsync(db, ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503))
            .DisableRateLimiting();
        api.MapGet("/public-settings", async (IOptions<PublicSettings> s, SettingsService settings, CancellationToken ct) =>
            Results.Ok(new PublicSettingsDto(s.Value.WhatsAppBusinessNumber, s.Value.BusinessTimeZone,
                (await settings.GetBookingSettingsAsync(ct)).FullRefundCancellationHours)));

        // ---------- Public catalog ----------
        var catalog = api.MapGroup("/catalog");
        catalog.MapGet("/stages", (CatalogQueries q, CancellationToken ct) => q.StagesWithGradesAsync(ct));
        catalog.MapGet("/study-tracks", (CatalogQueries q, CancellationToken ct) => q.StudyTracksAsync(ct));
        catalog.MapGet("/subjects", (Guid? gradeId, CatalogQueries q, CancellationToken ct) => q.SubjectsAsync(gradeId, ct));
        api.MapGet("/services", (CatalogQueries q, CancellationToken ct) => q.ServiceTypesAsync(ct));
        api.MapGet("/payment-methods", (CatalogQueries q, CancellationToken ct) => q.PaymentMethodsAsync(ct));

        // ---------- Public sessions ----------
        api.MapGet("/sessions", (Guid? gradeId, Guid? subjectId, Guid? studyTrackId, Guid? serviceTypeId, SessionMode? mode,
                int? page, SessionQueries q, CancellationToken ct) =>
            q.ListAsync(new SessionFilter(gradeId, subjectId, studyTrackId, serviceTypeId, mode, page ?? 1), ct));
        api.MapGet("/sessions/open-groups", (Guid? gradeId, Guid? subjectId, SessionQueries q, CancellationToken ct) =>
            q.ListAsync(new SessionFilter(gradeId, subjectId, null, null, SessionMode.Group), ct));
        api.MapGet("/sessions/{id:guid}", async (Guid id, SessionQueries q, CancellationToken ct) =>
            await q.GetAsync(id, ct) is { } s ? Results.Ok(s) : Results.NotFound());

        // ---------- Booking (authorized by the secret link token) ----------
        var booking = api.MapGroup("").RequireRateLimiting(BookingRateLimit);
        booking.MapPost("/bookings/holds", async (CreateHoldRequest req, BookingService svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateHoldAsync(req.SessionId, ct)));
        booking.MapGet("/b/{token}", async (string token, BookingService svc, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await svc.GetViewAsync(token, ct));
        });
        booking.MapPost("/b/{token}/details", async (string token, SubmitDetailsRequest req, BookingService svc, CancellationToken ct) =>
        {
            await svc.SubmitDetailsAsync(token, req, ct);
            return Results.NoContent();
        });
        booking.MapPost("/b/{token}/payment-proofs", async (string token, HttpRequest request, BookingService svc, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("screenshot");
            if (file is null || !Guid.TryParse(form["paymentMethodId"], out var methodId))
                return Results.Problem(title: "Screenshot and payment method are required.", statusCode: 400,
                    extensions: new Dictionary<string, object?> { ["code"] = BookingErrors.InvalidFile });
            await using var stream = file.OpenReadStream();
            await svc.SubmitPaymentProofAsync(token, methodId, stream, file.Length, form["senderReference"], ct);
            return Results.NoContent();
        }).DisableAntiforgery();
        booking.MapGet("/b/{token}/join", async (string token, BookingService svc, CancellationToken ct) =>
            Results.Ok(await svc.JoinAsync(token, ct)));

        // ---------- Auth (admin / teacher) ----------
        var auth = api.MapGroup("/auth");
        auth.MapPost("/login", async (LoginRequest req, SignInManager<IdentityUser> signIn) =>
        {
            var result = await signIn.PasswordSignInAsync(req.Email ?? "", req.Password ?? "", isPersistent: false, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Problem(title: "بيانات الدخول غير صحيحة.", statusCode: 401);
        }).RequireRateLimiting(LoginRateLimit);
        auth.MapPost("/logout", async (SignInManager<IdentityUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        });
        auth.MapGet("/me", (ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
            ? Results.Ok(new { email = user.Identity.Name, roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value) })
            : Results.Unauthorized());

        // ---------- Admin ----------
        var admin = api.MapGroup("/admin").RequireAuthorization(AdminPolicy);
        admin.MapGet("/payment-proofs", (PaymentProofStatus? status, PaymentReviewService svc, CancellationToken ct) =>
            svc.ListAsync(status, ct));
        admin.MapGet("/payment-proofs/{id:guid}/file", async (Guid id, PaymentReviewService svc, HttpContext http, CancellationToken ct) =>
        {
            var (content, type) = await svc.OpenFileAsync(id, ct);
            http.Response.Headers.CacheControl = "no-store";
            return Results.Stream(content, type);
        });
        admin.MapPost("/payment-proofs/{id:guid}/approve", async (Guid id, PaymentReviewService svc, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await svc.ApproveAsync(id, user.Identity!.Name!, ct);
            return Results.NoContent();
        });
        admin.MapPost("/payment-proofs/{id:guid}/reject", async (Guid id, RejectPaymentRequest req, PaymentReviewService svc, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await svc.RejectAsync(id, req.Reason, user.Identity!.Name!, ct);
            return Results.NoContent();
        });
        admin.MapGet("/settings", async (SettingsService svc, CancellationToken ct) =>
        {
            var values = await svc.GetAllAsync(ct);
            return values.Select(v => new SettingDto(v.Key, v.Value, SettingKeys.Definitions[v.Key].DescriptionAr));
        });
        admin.MapPut("/settings", async (UpdateSettingsRequest req, SettingsService svc, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await svc.UpdateAsync(req.Settings.ToDictionary(s => s.Key, s => s.Value), user.Identity!.Name!, ct);
            return Results.NoContent();
        });
    }
}

public sealed record LoginRequest(string? Email, string? Password);
