using System.Security.Claims;
using Dose.Application.Administration;
using Dose.Application.Notifications;
using Dose.Domain.Notifications;

namespace Dose.Api;

/// <summary>Message templates and log (admin), teacher photo upload (admin) and public teacher photos.</summary>
public static class MessagingEndpoints
{
    private static string Actor(ClaimsPrincipal u) => u.Identity!.Name!;

    public static void MapMessagingEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Endpoints.AdminPolicy);

        admin.MapGet("/messages", (NotificationStatus? status, NotificationService s, CancellationToken ct) => s.ListAsync(status, ct));
        admin.MapGet("/messages/{id:guid}/compose", async (Guid id, NotificationService s, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await s.ComposeAsync(id, ct));
        });
        admin.MapPost("/messages/{id:guid}/sent", async (Guid id, NotificationService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.MarkAsync(id, true, Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapPost("/messages/{id:guid}/skip", async (Guid id, NotificationService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.MarkAsync(id, false, Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapGet("/message-templates", (NotificationService s, CancellationToken ct) => s.TemplatesAsync(ct));
        admin.MapPut("/message-templates/{code}", async (string code, SaveTemplateRequest r, NotificationService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.SaveTemplateAsync(code, r, Actor(u), ct);
            return Results.NoContent();
        });

        admin.MapPost("/teachers/{id:guid}/photo", async (Guid id, HttpRequest request, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("photo");
            if (file is null) return Results.Problem(title: "Photo is required.", statusCode: 400);
            await using var stream = file.OpenReadStream();
            await s.UploadTeacherPhotoAsync(id, stream, file.Length, Actor(u), ct);
            return Results.NoContent();
        }).DisableAntiforgery();
        admin.MapDelete("/teachers/{id:guid}/photo", async (Guid id, AdminCatalogService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.RemoveTeacherPhotoAsync(id, Actor(u), ct);
            return Results.NoContent();
        });

        // Public: published teachers' photos (versioned URL → long cache).
        app.MapGet("/api/teachers/{id:guid}/photo", async (Guid id, AdminCatalogService s, HttpContext http, CancellationToken ct) =>
        {
            var photo = await s.OpenTeacherPhotoAsync(id, ct);
            if (photo is null) return Results.NotFound();
            http.Response.Headers.CacheControl = "public, max-age=604800";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Stream(photo.Value.Content, photo.Value.ContentType);
        });
    }
}
