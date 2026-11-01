using System.Security.Claims;
using Dose.Application.Administration;
using Dose.Application.Teachers;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Infrastructure.Identity;

namespace Dose.Api;

/// <summary>Phase 4: admin dashboard, user management, availability review, and the teacher area.</summary>
public static class Phase4Endpoints
{
    private static string Actor(ClaimsPrincipal u) => u.Identity!.Name!;
    private static string UserId(ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public static void MapPhase4Endpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Endpoints.AdminPolicy);
        admin.MapGet("/dashboard", (int? days, DashboardService s, CancellationToken ct) => s.GetAsync(days ?? 30, ct));

        admin.MapGet("/users", (UserAdminService s, CancellationToken ct) => s.ListAsync(ct));
        admin.MapPost("/users", async (CreateUserRequest r, UserAdminService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.CreateAsync(r, Actor(u), ct) }));
        admin.MapPost("/users/{id}/password", async (string id, ResetPasswordRequest r, UserAdminService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.ResetPasswordAsync(id, r.Password, Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapPost("/users/{id}/disabled", async (string id, SetDisabledRequest r, UserAdminService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.SetDisabledAsync(id, r.Disabled, UserId(u), Actor(u), ct);
            return Results.NoContent();
        });
        admin.MapPost("/users/{id}/teacher", async (string id, LinkTeacherRequest r, UserAdminService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.LinkTeacherAsync(id, r.TeacherId, Actor(u), ct);
            return Results.NoContent();
        });

        admin.MapGet("/availability", (string? status, TeacherAreaService s, CancellationToken ct) => s.AdminListAvailabilityAsync(status, ct));
        admin.MapPost("/availability/{id:guid}/review", async (Guid id, ReviewAvailabilityRequest r, TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.ReviewAvailabilityAsync(id, r, Actor(u), ct);
            return Results.NoContent();
        });

        // ---------- Teacher area ----------
        var teacher = app.MapGroup("/api/teacher").RequireAuthorization(Endpoints.TeacherPolicy);
        teacher.MapGet("/me", async (TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            var t = await s.TeacherForUserAsync(UserId(u), ct);
            return new TeacherMeDto(t.Id, t.DisplayNameAr, Actor(u));
        });
        teacher.MapGet("/sessions", (bool? past, TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
            s.SessionsAsync(UserId(u), past ?? false, ct));
        teacher.MapGet("/sessions/{id:guid}", (Guid id, TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
            s.DetailsAsync(UserId(u), id, ct));
        teacher.MapPost("/sessions/{id:guid}/attendance", async (Guid id, MarkAttendanceRequest r, TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.MarkAttendanceAsync(UserId(u), id, r, ct);
            return Results.NoContent();
        });
        teacher.MapPost("/sessions/{id:guid}/complete", async (Guid id, TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.CompleteAsync(UserId(u), id, ct);
            return Results.NoContent();
        });
        teacher.MapGet("/availability", (TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) => s.AvailabilityAsync(UserId(u), ct));
        teacher.MapPost("/availability", async (SubmitAvailabilityRequest r, TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
            Results.Ok(new { id = await s.SubmitAvailabilityAsync(UserId(u), r, ct) }));
        teacher.MapPost("/availability/{id:guid}/withdraw", async (Guid id, TeacherAreaService s, ClaimsPrincipal u, CancellationToken ct) =>
        {
            await s.WithdrawAvailabilityAsync(UserId(u), id, ct);
            return Results.NoContent();
        });
    }
}
