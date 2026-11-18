using System.Text.RegularExpressions;
using Dose.Domain.Administration;
using Dose.Domain.Common;
using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dose.Api;

public sealed record PledgeRequest(string? Organization, string? ContactName, string? ContactPhone, string? Role,
    string? Interest, int? Doses, bool ShowOnWall, string? Note, string? Source);
public sealed record PledgeResponse(string Reference);
public sealed record AdviceRequest(string? Message, string? Name, string? Phone, string? Source);
public sealed record WallEntry(string Organization, int Doses);
public sealed record PledgeWall(int TotalDoses, int Organizations, List<WallEntry> Names, DateTime? LastAtUtc);
public sealed record AdminPledge(Guid Id, string Reference, string Organization, string ContactName, string ContactPhone,
    string? Role, string Interest, int Doses, bool ShowOnWall, bool Hidden, string? Note, string? Source, DateTime CreatedAtUtc);

/// <summary>
/// Partner pledges from the public /partners page ("اكتب روشتة لطالب"): an organization pledges lesson doses for
/// students or asks to partner. Non-binding, no payment. The live wall shows the total and only the names that agreed.
/// </summary>
public sealed partial class PartnerService(DoseDbContext db)
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,59}$")]
    private static partial Regex SourcePattern();

    static string Required(string? v, int max, string code, string ar, string en)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v) || v.Length > max) throw new DomainException(code, ar, en);
        return v;
    }

    static string? Optional(string? v, int max)
    {
        v = v?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }

    public async Task<PartnerPledge> CreateAsync(PledgeRequest req, CancellationToken ct)
    {
        var org = Required(req.Organization, 120, "partner.organization", "اكتب اسم الجهة.", "Enter the organization's name.");
        var name = Required(req.ContactName, 100, "partner.name", "اكتب اسمك.", "Enter your name.");
        var phone = PhoneNumber.TryNormalize(req.ContactPhone)
                    ?? throw new DomainException("partner.phone", "رقم الموبايل مش صحيح.", "The mobile number is not valid.");
        var interest = req.Interest?.Trim().ToLowerInvariant() ?? "";
        if (!PartnerPledge.Interests.Contains(interest))
            throw new DomainException("partner.interest", "اختار نوع الشراكة.", "Choose a partnership type.");
        var doses = req.Doses ?? 0;
        if (doses < 0 || doses > PartnerPledge.MaxDoses)
            throw new DomainException("partner.doses", "عدد الجرعات مش صحيح.", "The number of doses is not valid.");
        if (interest == "fund" && doses == 0)
            throw new DomainException("partner.doses", "اكتب عدد الجرعات اللي حابين تتعهدوا بيها.", "Enter how many doses you'd like to pledge.");
        var source = req.Source?.Trim().ToLowerInvariant();
        if (source is not null && !SourcePattern().IsMatch(source)) source = null;

        var p = new PartnerPledge(org, name, phone, Optional(req.Role, 100), interest, doses, req.ShowOnWall,
            Optional(req.Note, 1000), source, DateTime.UtcNow);
        db.Add(p);
        await db.SaveChangesAsync(ct);
        return p;
    }

    /// <summary>Advice or a comment from a visitor (event QR page). Only the message is required.</summary>
    public async Task<PartnerPledge> AdviceAsync(AdviceRequest req, CancellationToken ct)
    {
        var message = Required(req.Message, 1000, "partner.advice", "اكتب نصيحتك أو تعليقك.", "Write your advice or comment.");
        var phone = string.IsNullOrWhiteSpace(req.Phone) ? "" : PhoneNumber.TryNormalize(req.Phone)
                    ?? throw new DomainException("partner.phone", "رقم الموبايل مش صحيح.", "The mobile number is not valid.");
        var source = req.Source?.Trim().ToLowerInvariant();
        if (source is not null && !SourcePattern().IsMatch(source)) source = null;
        var p = new PartnerPledge("—", Optional(req.Name, 100) ?? "زائر", phone, null, PartnerPledge.Advice, 0, false, message, source, DateTime.UtcNow);
        db.Add(p);
        await db.SaveChangesAsync(ct);
        return p;
    }

    /// <summary>Public wall: total doses pledged and the names of organizations that agreed to be shown.</summary>
    public async Task<PledgeWall> WallAsync(string? source, CancellationToken ct)
    {
        var q = db.PartnerPledges.AsNoTracking().Where(p => !p.Hidden && p.Doses > 0);
        source = source?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(source) && SourcePattern().IsMatch(source)) q = q.Where(p => p.Source != null && p.Source.StartsWith(source));
        var rows = await q.Select(p => new { p.Organization, p.Doses, p.ShowOnWall, p.CreatedAtUtc }).ToListAsync(ct);
        var names = rows.Where(r => r.ShowOnWall)
            .GroupBy(r => r.Organization.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new WallEntry(g.First().Organization, g.Sum(r => r.Doses)))
            .OrderByDescending(e => e.Doses).ToList();
        var orgs = rows.Select(r => r.Organization.Trim().ToLowerInvariant()).Distinct().Count();
        return new PledgeWall(rows.Sum(r => r.Doses), orgs, names, rows.Count == 0 ? null : rows.Max(r => r.CreatedAtUtc));
    }

    public Task<List<AdminPledge>> ListAsync(CancellationToken ct) =>
        db.PartnerPledges.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new AdminPledge(p.Id, p.Reference, p.Organization, p.ContactName, p.ContactPhoneE164, p.Role,
                p.Interest, p.Doses, p.ShowOnWall, p.Hidden, p.Note, p.Source, p.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<bool> SetHiddenAsync(Guid id, bool hidden, CancellationToken ct)
    {
        var p = await db.PartnerPledges.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        p.Hidden = hidden;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public static string InterestAr(string interest) => interest switch
    {
        "fund" => "تمويل جرعات لطلاب",
        "students" => "عندنا طلاب محتاجين دعم",
        "employees" => "ميزة لأولاد الموظفين",
        "content" => "شراكة محتوى / توعية",
        PartnerPledge.Advice => "نصيحة / تعليق",
        _ => "حاجة تانية",
    };
}

public static class PartnerEndpoints
{
    public static void MapPartnerEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        api.MapPost("/partners/pledges", async (PledgeRequest req, PartnerService s, OwnerAlerts alerts, CancellationToken ct) =>
        {
            var p = await s.CreateAsync(req, ct);
            await alerts.PledgeAsync(p.Id);
            return new PledgeResponse(p.Reference);
        }).RequireRateLimiting(Endpoints.BookingRateLimit);
        api.MapPost("/partners/advice", async (AdviceRequest req, PartnerService s, OwnerAlerts alerts, CancellationToken ct) =>
        {
            var p = await s.AdviceAsync(req, ct);
            await alerts.AdviceAsync(p.Id);
            return new PledgeResponse(p.Reference);
        }).RequireRateLimiting(Endpoints.BookingRateLimit);
        api.MapGet("/partners/wall", (string? src, PartnerService s, CancellationToken ct) => s.WallAsync(src, ct));

        var admin = api.MapGroup("/admin/partners").RequireAuthorization(Endpoints.AdminPolicy);
        admin.MapGet("", (PartnerService s, CancellationToken ct) => s.ListAsync(ct));
        admin.MapPost("/{id:guid}/hidden", async (Guid id, bool hidden, PartnerService s, CancellationToken ct) =>
            await s.SetHiddenAsync(id, hidden, ct) ? Results.NoContent() : Results.NotFound());
    }
}
