using System.Security.Claims;
using Dose.Application.Abstractions;
using Dose.Application.Bookings;
using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dose.Api;

public sealed record LookupBooking(string Reference, string Status, string Subject, string Scope, DateTime StartUtc, DateTime CreatedAtUtc);
public sealed record LookupRequest(string Reference, string Status, string Subject, string Grade, string Scope, DateTime CreatedAtUtc);
public sealed record LookupLink(DateTime CreatedAtUtc, DateTime ExpiresAtUtc, int Uses, DateTime? LastUsedAtUtc, string CreatedBy);
public sealed record StudentLookup(string Phone, string? Name, List<LookupBooking> Bookings, List<LookupRequest> Requests, List<LookupLink> Links);
public sealed record IssueAccessLinkRequest(string? Phone);
public sealed record IssuedAccessLink(string Link, string Message, string ChatUrl, DateTime ExpiresAtUtc);
public sealed record RestoredBooking(string Reference, string Token);
public sealed record RestoredRequest(string Reference, string Subject, string Grade, DateTime SentAtUtc);
public sealed record RestoredItems(List<RestoredBooking> Bookings, List<RestoredRequest> Requests);

/// <summary>
/// "Send me my bookings on WhatsApp": a student who lost their booking links (new phone, cleared browser, booked inside
/// Instagram's in-app browser) messages us from their own WhatsApp. The team looks the number up and sends back one
/// private link that restores every booking and request made with that number on any device. No accounts, no passwords.
/// </summary>
public sealed class StudentAccessService(DoseDbContext db, ITokenProtector tokens, IOptions<PublicSettings> site)
{
    static string Phone(string? raw) => PhoneNumber.TryNormalize(raw)
        ?? throw new DomainException("student.phone", "رقم الموبايل مش صحيح.", "The mobile number is not valid.");

    IQueryable<Booking> BookingsFor(string phone) => db.Bookings.AsNoTracking()
        .Where(b => b.Status != BookingStatus.Expired)
        .Where(b => b.ContactPhoneE164 == phone || (b.Student != null && (b.Student.ContactPhoneE164 == phone || b.Student.GuardianPhoneE164 == phone)));

    IQueryable<Domain.Bookings.SessionRequest> RequestsFor(string phone) => db.SessionRequests.AsNoTracking()
        .Where(r => r.ContactPhoneE164 == phone || r.GuardianPhoneE164 == phone);

    public async Task<StudentLookup> LookupAsync(string? rawPhone, CancellationToken ct)
    {
        var phone = Phone(rawPhone);
        var bookings = await BookingsFor(phone).OrderByDescending(b => b.CreatedAtUtc).Take(50)
            .Select(b => new LookupBooking(b.Reference, b.Status.ToString(), b.Session!.Subject!.NameAr, b.Session.ScopeTitleAr, b.Session.StartUtc, b.CreatedAtUtc))
            .ToListAsync(ct);
        var requests = await (from r in RequestsFor(phone)
                              join s in db.Subjects on r.SubjectId equals s.Id
                              join g in db.Grades on r.GradeId equals g.Id
                              orderby r.CreatedAtUtc descending
                              select new LookupRequest(r.Reference, r.Status.ToString(), s.NameAr, g.NameAr, r.ScopeText, r.CreatedAtUtc)).Take(50).ToListAsync(ct);
        var name = await BookingsFor(phone).Where(b => b.Student != null).OrderByDescending(b => b.CreatedAtUtc).Select(b => b.Student!.Name).FirstOrDefaultAsync(ct)
                   ?? await RequestsFor(phone).OrderByDescending(r => r.CreatedAtUtc).Select(r => r.StudentName).FirstOrDefaultAsync(ct);
        var links = await db.StudentAccessLinks.AsNoTracking().Where(l => l.PhoneE164 == phone).OrderByDescending(l => l.CreatedAtUtc).Take(10)
            .Select(l => new LookupLink(l.CreatedAtUtc, l.ExpiresAtUtc, l.Uses, l.LastUsedAtUtc, l.CreatedBy)).ToListAsync(ct);
        return new StudentLookup(phone, name, bookings, requests, links);
    }

    public async Task<IssuedAccessLink> IssueAsync(string? rawPhone, string actor, CancellationToken ct)
    {
        var lookup = await LookupAsync(rawPhone, ct);
        if (lookup.Bookings.Count == 0 && lookup.Requests.Count == 0)
            throw new DomainException("student.none", "مفيش حجوزات ولا طلبات بالرقم ده.", "There are no bookings or requests with this number.");
        var (token, hash) = AccessTokens.Create();
        var link = new StudentAccessLink(hash, lookup.Phone, actor, DateTime.UtcNow, StudentAccessLink.Validity);
        db.Add(link);
        await db.SaveChangesAsync(ct);
        var url = $"{site.Value.AppBaseUrl.TrimEnd('/')}/me/{token}";
        var first = lookup.Name?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var message = $"أهلاً{(first is null ? "" : " " + first)} 👋\n"
                    + "ده لينك حجوزاتك في دروس Dose. افتحه من الموبايل اللي بتستخدمه، وهتلاقي كل حجوزاتك وطلباتك في «حجوزاتي»:\n"
                    + url + "\n\n🔒 اللينك ده خاص بيك، متبعتوش لحد.";
        var chat = $"https://wa.me/{new string(lookup.Phone.Where(char.IsDigit).ToArray())}";
        return new IssuedAccessLink(url, message, chat, link.ExpiresAtUtc);
    }

    /// <summary>Public: the bookings (with their private links) and requests for the link's number, or null.</summary>
    public async Task<RestoredItems?> RestoreAsync(string token, CancellationToken ct)
    {
        if (!AccessTokens.LooksValid(token)) return null;
        var hash = AccessTokens.Hash(token);
        var link = await db.StudentAccessLinks.FirstOrDefaultAsync(l => l.TokenHash == hash, ct);
        if (link is null || link.ExpiresAtUtc < DateTime.UtcNow) return null;
        link.Uses++;
        link.LastUsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var rows = await BookingsFor(link.PhoneE164).OrderByDescending(b => b.CreatedAtUtc).Take(50)
            .Select(b => new { b.Reference, b.AccessTokenProtected }).ToListAsync(ct);
        var bookings = rows.Select(r => r.AccessTokenProtected is null ? null : tokens.Unprotect(r.AccessTokenProtected) is { } t ? new RestoredBooking(r.Reference, t) : null)
            .OfType<RestoredBooking>().ToList();
        var requests = await (from r in RequestsFor(link.PhoneE164)
                              join s in db.Subjects on r.SubjectId equals s.Id
                              join g in db.Grades on r.GradeId equals g.Id
                              orderby r.CreatedAtUtc descending
                              select new RestoredRequest(r.Reference, s.NameAr, g.NameAr, r.CreatedAtUtc)).Take(50).ToListAsync(ct);
        return new RestoredItems(bookings, requests);
    }
}

public static class StudentAccessEndpoints
{
    public static void MapStudentAccessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me/{token}", async (string token, StudentAccessService s, CancellationToken ct) =>
                await s.RestoreAsync(token, ct) is { } items ? Results.Ok(items) : Results.NotFound())
            .RequireRateLimiting(Endpoints.BookingRateLimit);

        var admin = app.MapGroup("/api/admin/students").RequireAuthorization(Endpoints.AdminPolicy);
        admin.MapGet("/lookup", (string? phone, StudentAccessService s, CancellationToken ct) => s.LookupAsync(phone, ct));
        admin.MapPost("/access-link", (IssueAccessLinkRequest req, StudentAccessService s, ClaimsPrincipal u, CancellationToken ct) =>
            s.IssueAsync(req.Phone, u.Identity!.Name!, ct));
    }
}
