using System.Security.Claims;
using Dose.Application.Administration;
using Dose.Contracts.Admin;

namespace Dose.Api;

/// <summary>Phase 2 admin configuration endpoints. All require the Admin role.</summary>
public static class AdminEndpoints
{
    private static string Actor(ClaimsPrincipal u) => u.Identity!.Name!;

    public static void MapAdminConfigEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Endpoints.AdminPolicy);

        // ---------- Catalog ----------
        var c = admin.MapGroup("/catalog");
        c.MapGet("/stages", (AdminCatalogService s, CancellationToken ct) => s.StagesAsync(ct));
        c.MapPost("/stages", async (StageAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveStageAsync(d, Actor(u), ct) }));
        c.MapGet("/study-tracks", (AdminCatalogService s, CancellationToken ct) => s.StudyTracksAsync(ct));
        c.MapPost("/study-tracks", async (LookupAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveStudyTrackAsync(d, Actor(u), ct) }));
        c.MapGet("/pricing-bands", (AdminCatalogService s, CancellationToken ct) => s.PricingBandsAsync(ct));
        c.MapPost("/pricing-bands", async (PricingBandAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SavePricingBandAsync(d, Actor(u), ct) }));
        c.MapGet("/subjects", (AdminCatalogService s, CancellationToken ct) => s.SubjectsAsync(ct));
        c.MapPost("/subjects", async (LookupAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveSubjectAsync(d, Actor(u), ct) }));
        c.MapGet("/grades", (AdminCatalogService s, CancellationToken ct) => s.GradesAsync(ct));
        c.MapPost("/grades", async (GradeAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveGradeAsync(d, Actor(u), ct) }));
        c.MapGet("/chapters", (Guid? gradeId, Guid? subjectId, AdminCatalogService s, CancellationToken ct) => s.ChaptersAsync(gradeId, subjectId, ct));
        c.MapPost("/chapters", async (ChapterAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveChapterAsync(d, Actor(u), ct) }));
        c.MapGet("/lessons", (Guid chapterId, AdminCatalogService s, CancellationToken ct) => s.LessonsAsync(chapterId, ct));
        c.MapPost("/lessons", async (LessonAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveLessonAsync(d, Actor(u), ct) }));
        c.MapGet("/service-types", (AdminCatalogService s, CancellationToken ct) => s.ServiceTypesAsync(ct));
        c.MapPost("/service-types", async (ServiceTypeAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.SaveServiceTypeAsync(d, Actor(u), ct);
            return Results.Ok(new { id = d.Id });
        });

        // ---------- Teachers & payment methods ----------
        admin.MapGet("/teachers", (AdminCatalogService s, CancellationToken ct) => s.TeachersAsync(ct));
        admin.MapPost("/teachers", async (TeacherAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SaveTeacherAsync(d, Actor(u), ct) }));
        admin.MapGet("/payment-methods", (AdminCatalogService s, CancellationToken ct) => s.PaymentMethodsAsync(ct));
        admin.MapPost("/payment-methods", async (PaymentMethodAdminDto d, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SavePaymentMethodAsync(d, Actor(u), ct) }));

        // ---------- Pricing ----------
        admin.MapGet("/pricing/plans", (AdminPricingService s, CancellationToken ct) => s.PlansAsync(ct));
        admin.MapGet("/pricing/plans/{id:guid}", (Guid id, AdminPricingService s, CancellationToken ct) => s.PlanAsync(id, ct));
        admin.MapPost("/pricing/plans", async (PricingPlanAdminDto d, AdminPricingService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SavePlanAsync(d, Actor(u), ct) }));
        admin.MapPost("/pricing/plans/{id:guid}/prices", (Guid id, SavePricesRequest d, AdminPricingService s, ClaimsPrincipal u, CancellationToken ct) =>
            s.SavePricesAsync(id, d, Actor(u), ct));

        // ---------- Sessions ----------
        var sessions = admin.MapGroup("/sessions");
        sessions.MapGet("", (string? status, bool? includePast, AdminSessionService s, CancellationToken ct) => s.ListAsync(status, includePast ?? false, ct));
        sessions.MapGet("/{id:guid}", (Guid id, AdminSessionService s, CancellationToken ct) => s.GetAsync(id, ct));
        sessions.MapPost("/pricing-preview", (PricingPreviewRequest d, AdminSessionService s, CancellationToken ct) => s.PreviewPricingAsync(d, ct));
        sessions.MapPost("", async (SessionSaveRequest d, AdminSessionService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.CreateAsync(d, Actor(u), ct) }));
        sessions.MapPut("/{id:guid}", async (Guid id, SessionSaveRequest d, AdminSessionService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.UpdateAsync(id, d, Actor(u), ct);
            return Results.NoContent();
        });
        sessions.MapPost("/{id:guid}/publish", async (Guid id, AdminSessionService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.PublishAsync(id, Actor(u), ct);
            return Results.NoContent();
        });
        sessions.MapPost("/{id:guid}/cancel", async (Guid id, CancelRequest d, AdminSessionService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { refundsPending = await s.CancelAsync(id, d.Reason, Actor(u), ct) }));
        sessions.MapGet("/{id:guid}/bookings", (Guid id, AdminSessionService s, CancellationToken ct) => s.BookingsAsync(id, ct));
        admin.MapPost("/bookings/{id:guid}/reissue-link", (Guid id, AdminSessionService s, ClaimsPrincipal u, CancellationToken ct) =>
            s.ReissueLinkAsync(id, Actor(u), ct));
    }
}

public sealed record CancelRequest(string Reason);
