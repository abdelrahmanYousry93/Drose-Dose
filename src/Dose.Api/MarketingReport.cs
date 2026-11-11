using System.Text.Json;
using System.Text.RegularExpressions;
using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dose.Api;

/// <summary>
/// The marketing funnel per post: visits from the post's link, WhatsApp clicks, bookings started, requests sent and
/// bookings paid — plus each published post's engagement read from Facebook and Instagram. Aggregates only.
/// </summary>
public sealed partial class MarketingReport(
    DoseDbContext db, IHttpClientFactory http, IOptions<MetaSettings> meta, MarketingPublisher publisher, ILogger<MarketingReport> log)
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,59}$")]
    private static partial Regex SourcePattern();
    [GeneratedRegex("^[A-Z0-9-]{4,40}$")]
    private static partial Regex ReferencePattern();

    static readonly BookingStatus[] Paid =
        [BookingStatus.Confirmed, BookingStatus.Completed, BookingStatus.TopUpPending, BookingStatus.Transferred];

    /// <summary>Records one funnel step. Unknown or malformed input is ignored (returns false), never an error.</summary>
    public async Task<bool> TrackAsync(string? source, string? step, string? reference, CancellationToken ct)
    {
        source = source?.Trim().ToLowerInvariant();
        reference = reference?.Trim().ToUpperInvariant();
        if (source is null || !SourcePattern().IsMatch(source) || step is null || !MarketingTouch.Steps.Contains(step)) return false;
        if (reference is not null && !ReferencePattern().IsMatch(reference)) reference = null;
        db.Add(new MarketingTouch(source, step, reference, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
        return true;
    }

    public sealed record SourceRow(string Source, int Visits, int WhatsApp, int Holds, int Requests, int Paid);

    public async Task<List<SourceRow>> FunnelAsync(DateTime sinceUtc, CancellationToken ct)
    {
        var touches = await db.Set<MarketingTouch>().AsNoTracking().Where(t => t.CreatedAtUtc >= sinceUtc)
            .Select(t => new { t.Source, t.Step, t.Reference }).ToListAsync(ct);
        var holdRefs = touches.Where(t => t.Step == "hold" && t.Reference != null).Select(t => t.Reference!).Distinct().ToList();
        var paidRefs = holdRefs.Count == 0 ? [] : (await db.Set<Booking>().AsNoTracking()
            .Where(b => holdRefs.Contains(b.Reference) && Paid.Contains(b.Status)).Select(b => b.Reference).ToListAsync(ct)).ToHashSet();
        return touches.GroupBy(t => t.Source).Select(g => new SourceRow(
                g.Key,
                g.Count(t => t.Step == "visit"),
                g.Count(t => t.Step == "whatsapp"),
                g.Count(t => t.Step == "hold"),
                g.Count(t => t.Step == "request"),
                g.Where(t => t.Step == "hold" && t.Reference != null).Select(t => t.Reference!).Distinct().Count(paidRefs.Contains)))
            .OrderByDescending(r => r.Paid).ThenByDescending(r => r.Visits).ToList();
    }

    static (DateTime At, Dictionary<string, object> Data)? engagementCache;

    /// <summary>Likes, comments, shares (and reach where the key allows it) for each published post; cached 30 minutes.</summary>
    public async Task<Dictionary<string, object>> EngagementAsync(CancellationToken ct)
    {
        if (!meta.Value.Enabled) return [];
        if (engagementCache is { } c && c.At > DateTime.UtcNow.AddMinutes(-30)) return c.Data;
        var m = meta.Value;
        var client = http.CreateClient("meta");
        var token = await publisher.PageTokenForReadAsync(ct);
        var posts = await db.Set<MarketingPost>().AsNoTracking()
            .Where(p => p.PublishedAtUtc != null && p.ExternalId != null && !p.ExternalId.StartsWith("rescrape:"))
            .OrderByDescending(p => p.PublishedAtUtc).Take(60).ToListAsync(ct);
        var data = new Dictionary<string, object>();

        async Task<JsonElement?> Get(string path)
        {
            try
            {
                using var res = await client.GetAsync($"https://graph.facebook.com/{m.GraphVersion}/{path}&access_token={Uri.EscapeDataString(token)}", ct);
                if (!res.IsSuccessStatusCode) return null;
                return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement.Clone();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogDebug(ex, "Engagement read failed for {Path}", path);
                return null;
            }
        }
        static int? Count(JsonElement? j, string prop, string? inner = null)
        {
            if (j is not { } e || !e.TryGetProperty(prop, out var p)) return null;
            if (inner is null) return p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
            return p.TryGetProperty("summary", out var s) && s.TryGetProperty(inner, out var n) ? n.GetInt32() : null;
        }
        static int? InsightValue(JsonElement? j, string metric)
        {
            if (j is not { } e || !e.TryGetProperty("data", out var arr)) return null;
            foreach (var item in arr.EnumerateArray())
                if (item.GetProperty("name").GetString() == metric && item.TryGetProperty("values", out var v) && v.GetArrayLength() > 0)
                    return v[0].GetProperty("value").ValueKind == JsonValueKind.Number ? v[0].GetProperty("value").GetInt32() : null;
            return null;
        }

        foreach (var p in posts)
        {
            if (p.Channel == "facebook")
            {
                var j = await Get($"{p.ExternalId}?fields=reactions.summary(true).limit(0),comments.summary(true).limit(0),shares");
                var ins = await Get($"{p.ExternalId}/insights?metric=post_impressions_unique,post_clicks");
                data[$"{p.Key}|facebook"] = new
                {
                    reactions = Count(j, "reactions", "total_count"), comments = Count(j, "comments", "total_count"),
                    shares = j is { } e && e.TryGetProperty("shares", out var sh) && sh.TryGetProperty("count", out var sc) ? sc.GetInt32() : 0,
                    reach = InsightValue(ins, "post_impressions_unique"), clicks = InsightValue(ins, "post_clicks"),
                };
            }
            else
            {
                var j = await Get($"{p.ExternalId}?fields=like_count,comments_count");
                var ins = await Get($"{p.ExternalId}/insights?metric=reach,saved,shares");
                data[$"{p.Key}|instagram"] = new
                {
                    likes = Count(j, "like_count"), comments = Count(j, "comments_count"),
                    reach = InsightValue(ins, "reach"), saves = InsightValue(ins, "saved"), shares = InsightValue(ins, "shares"),
                };
            }
        }
        engagementCache = (DateTime.UtcNow, data);
        return data;
    }
}
