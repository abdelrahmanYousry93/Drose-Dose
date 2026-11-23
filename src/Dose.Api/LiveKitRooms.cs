using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dose.Application.Bookings;
using Dose.Application.Teachers;
using Dose.Contracts;
using Dose.Domain.Sessions;
using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dose.Api;

/// <summary>
/// Dose's own video room on LiveKit Cloud. Reads LIVEKIT_URL / LIVEKIT_API_KEY / LIVEKIT_API_SECRET (set on the server only;
/// the secret never reaches a browser). Issues short-lived room passes (HS256 JWT, the LiveKit access-token format) and calls
/// LiveKit's RoomService for host actions (mute a student, remove someone).
/// Students: one participant per booking (a second device with the same link replaces the first), camera and microphone.
/// Teacher / admin: host — camera, screen share, room admin.
/// </summary>
public sealed class LiveKitRooms(IConfiguration config, IHttpClientFactory http)
{
    string? Url => config["LIVEKIT_URL"];
    string? Key => config["LIVEKIT_API_KEY"];
    string? Secret => config["LIVEKIT_API_SECRET"];

    public bool Configured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(Secret);

    public RoomAccessDto Pass(Guid sessionId, string identity, string name, bool host, string? title, string? teacher, DateTime endUtc)
    {
        if (!Configured) throw new InvalidOperationException("LiveKit is not configured.");
        var room = DoseRoom.RoomName(sessionId);
        var role = host ? "host" : "student";
        var now = DateTimeOffset.UtcNow;
        var exp = new DateTimeOffset(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc)).AddHours(1);
        if (exp < now.AddHours(1)) exp = now.AddHours(1);
        var video = new Dictionary<string, object>
        {
            ["room"] = room, ["roomJoin"] = true, ["canSubscribe"] = true, ["canPublish"] = true, ["canPublishData"] = true,
            ["canPublishSources"] = host ? new[] { "camera", "microphone", "screen_share", "screen_share_audio" } : new[] { "camera", "microphone" },
        };
        if (host) video["roomAdmin"] = true;
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Key!, ["sub"] = identity, ["name"] = name, ["nbf"] = now.AddSeconds(-30).ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(), ["jti"] = Guid.NewGuid().ToString("N"), ["video"] = video,
            ["metadata"] = JsonSerializer.Serialize(new { role }),
        };
        return new RoomAccessDto(Url!, Sign(claims), identity, name, role, title, teacher, endUtc);
    }

    /// <summary>Host action through LiveKit's RoomService (server side, so the browser never holds an admin key).</summary>
    public async Task ModerateAsync(Guid sessionId, RoomModerateRequest req, CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("LiveKit is not configured.");
        var room = DoseRoom.RoomName(sessionId);
        var identity = req.Identity ?? "";
        if (!identity.StartsWith("b-", StringComparison.Ordinal)) throw new ArgumentException("Only students can be moderated.");
        var (method, body) = req.Action switch
        {
            "mute" when !string.IsNullOrWhiteSpace(req.TrackSid) =>
                ("MutePublishedTrack", (object)new { room, identity, track_sid = req.TrackSid, muted = true }),
            "remove" => ("RemoveParticipant", new { room, identity }),
            _ => throw new ArgumentException("Unknown action."),
        };
        var now = DateTimeOffset.UtcNow;
        var token = Sign(new Dictionary<string, object>
        {
            ["iss"] = Key!, ["nbf"] = now.AddSeconds(-30).ToUnixTimeSeconds(), ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["video"] = new Dictionary<string, object> { ["room"] = room, ["roomAdmin"] = true },
        });
        var baseUrl = Url!.Replace("wss://", "https://").Replace("ws://", "http://").TrimEnd('/');
        using var msg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/twirp/livekit.RoomService/{method}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.CreateClient("livekit").SendAsync(msg, ct);
        res.EnsureSuccessStatusCode();
    }

    string Sign(Dictionary<string, object> claims)
    {
        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var head = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var body = B64(JsonSerializer.SerializeToUtf8Bytes(claims));
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret!));
        return $"{head}.{body}.{B64(h.ComputeHash(Encoding.UTF8.GetBytes($"{head}.{body}")))}";
    }
}

public static class RoomEndpoints
{
    static IResult NotReady() => Results.Problem(title: "غرفة دروس Dose مش جاهزة لسه. استخدم رابط الجلسة العادي.", statusCode: 503);

    public static void MapRoomEndpoints(this WebApplication app)
    {
        // Student: the booking's secret link is the key. Same checks as Join (confirmed, window open).
        app.MapGet("/api/b/{token}/room", async (string token, BookingService svc, LiveKitRooms lk, HttpContext http, CancellationToken ct) =>
        {
            if (!lk.Configured) return NotReady();
            var seat = await svc.RoomSeatAsync(token, JoinCodes.Device(http.Request.Headers.UserAgent.ToString(), http.Connection.RemoteIpAddress?.ToString()), ct);
            return Results.Ok(lk.Pass(seat.SessionId, "b-" + seat.BookingId.ToString("N"), seat.StudentName, host: false, seat.Title, seat.TeacherName, seat.EndUtc));
        });

        // Teacher of the session, or an admin: host pass.
        app.MapGet("/api/room/{sessionId:guid}/host", async (Guid sessionId, ClaimsPrincipal u, DoseDbContext db, TeacherAreaService teachers, LiveKitRooms lk, CancellationToken ct) =>
        {
            if (!lk.Configured) return NotReady();
            var s = await HostSessionAsync(sessionId, u, db, teachers, ct);
            if (s is null) return Results.NotFound();
            var name = u.IsInRole(DevelopmentSeeder.AdminRole) && s.TeacherUserId != UserId(u) ? "إدارة دروس Dose" : s.TeacherName ?? "المدرس";
            return Results.Ok(lk.Pass(sessionId, u.IsInRole(DevelopmentSeeder.AdminRole) && s.TeacherUserId != UserId(u) ? "admin" : "teacher", name, host: true, s.Title, s.TeacherName, s.EndUtc));
        }).RequireAuthorization();

        app.MapPost("/api/room/{sessionId:guid}/moderate", async (Guid sessionId, RoomModerateRequest req, ClaimsPrincipal u, DoseDbContext db, TeacherAreaService teachers, LiveKitRooms lk, CancellationToken ct) =>
        {
            if (!lk.Configured) return NotReady();
            if (await HostSessionAsync(sessionId, u, db, teachers, ct) is null) return Results.NotFound();
            try { await lk.ModerateAsync(sessionId, req, ct); }
            catch (ArgumentException e) { return Results.Problem(title: e.Message, statusCode: 400); }
            return Results.NoContent();
        }).RequireAuthorization();
    }

    static string? UserId(ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.NameIdentifier);

    sealed record HostSession(string? Title, string? TeacherName, string? TeacherUserId, DateTime EndUtc);

    static async Task<HostSession?> HostSessionAsync(Guid sessionId, ClaimsPrincipal u, DoseDbContext db, TeacherAreaService teachers, CancellationToken ct)
    {
        var s = await db.Sessions.AsNoTracking().Where(x => x.Id == sessionId && x.MeetingUrl == DoseRoom.Marker)
            .Select(x => new HostSession(x.ScopeTitleAr, x.Teacher!.DisplayNameAr, x.Teacher.UserId, x.EndUtc)).FirstOrDefaultAsync(ct);
        if (s is null) return null;
        if (u.IsInRole(DevelopmentSeeder.AdminRole)) return s;
        return s.TeacherUserId is not null && s.TeacherUserId == UserId(u) ? s : null;
    }
}
