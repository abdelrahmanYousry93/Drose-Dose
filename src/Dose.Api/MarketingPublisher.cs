using System.Text.Json;
using System.Text.Json.Serialization;
using Dose.Domain.Administration;
using Dose.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dose.Api;

/// <summary>Meta (Facebook Page) publishing settings. The token is a secret: set it only as an encrypted environment variable.</summary>
public sealed class MetaSettings
{
    public const string SectionName = "Meta";
    public string? PageId { get; init; }
    public string? PageAccessToken { get; init; }
    public string GraphVersion { get; init; } = "v27.0";
    /// <summary>Queue file written by the marketing agency; copied into the image by the Dockerfile.</summary>
    public string QueuePath { get; init; } = "marketing/queue.json";
    public bool Enabled => !string.IsNullOrWhiteSpace(PageId) && !string.IsNullOrWhiteSpace(PageAccessToken);
}

/// <summary>An approved post waiting for its time. Media are site-relative paths under /m/ (public JPEG/MP4 files).</summary>
public sealed record QueuedPost(
    string Key, string Kind, string? Caption, string[] Media, DateTimeOffset PublishAt, string[]? Channels);

/// <summary>
/// Publishes approved marketing posts to the Dose Facebook Page when their time comes. The agency only ever adds posts
/// the owner approved in the agency HQ; the queue ships with each deploy. Each key is published at most once,
/// failures are retried up to 3 times, and nothing older than 2 days is published late.
/// </summary>
public sealed class MarketingPublisher(
    DoseDbContext db, IHttpClientFactory http, IOptions<MetaSettings> meta, IOptions<PublicSettings> site,
    IWebHostEnvironment env, ILogger<MarketingPublisher> log)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    public IReadOnlyList<QueuedPost> ReadQueue()
    {
        var path = Path.IsPathRooted(meta.Value.QueuePath) ? meta.Value.QueuePath : Path.Combine(env.ContentRootPath, meta.Value.QueuePath);
        if (!File.Exists(path)) return [];
        try { return JsonSerializer.Deserialize<List<QueuedPost>>(File.ReadAllText(path), Json) ?? []; }
        catch (JsonException ex) { log.LogError(ex, "Marketing queue {Path} is not valid JSON", path); return []; }
    }

    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken ct)
    {
        if (!meta.Value.Enabled) return;
        var now = DateTime.UtcNow;
        var due = ReadQueue()
            .Where(p => (p.Channels is null || p.Channels.Contains("facebook")) && p.PublishAt.UtcDateTime <= now
                        && p.PublishAt.UtcDateTime > now.AddDays(-2))
            .OrderBy(p => p.PublishAt).ToList();
        foreach (var post in due)
        {
            var row = await db.Set<MarketingPost>().FirstOrDefaultAsync(x => x.Key == post.Key && x.Channel == "facebook", ct);
            if (row is { PublishedAtUtc: not null } || row?.Attempts >= 3) continue;
            if (row is null) { row = new MarketingPost(post.Key, "facebook", now); db.Add(row); }
            try
            {
                row.Published(await PublishToPageAsync(post, ct), DateTime.UtcNow);
                log.LogInformation("Published marketing post {Key} to the Facebook Page", post.Key);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                row.Failed(ex.Message);
                log.LogError(ex, "Publishing marketing post {Key} failed", post.Key);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    async Task<string> PublishToPageAsync(QueuedPost post, CancellationToken ct)
    {
        var m = meta.Value;
        var baseUrl = $"https://graph.facebook.com/{m.GraphVersion}/{m.PageId}";
        string Url(string media) => media.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? media : $"{site.Value.AppBaseUrl.TrimEnd('/')}/{media.TrimStart('/')}";
        var client = http.CreateClient("meta");

        async Task<JsonElement> Post(string url, Dictionary<string, string> form)
        {
            form["access_token"] = m.PageAccessToken!;
            using var res = await client.PostAsync(url, new FormUrlEncodedContent(form), ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body).RootElement.Clone();
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(json.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var msg)
                    ? $"Meta {(int)res.StatusCode}: {msg.GetString()}" : $"Meta {(int)res.StatusCode}");
            return json;
        }
        static string Id(JsonElement j) => j.TryGetProperty("post_id", out var p) ? p.GetString()! : j.GetProperty("id").GetString()!;

        switch (post.Kind)
        {
            case "photo":
                return Id(await Post($"{baseUrl}/photos", new() { ["url"] = Url(post.Media[0]), ["message"] = post.Caption ?? "" }));
            case "album":
            {
                var form = new Dictionary<string, string> { ["message"] = post.Caption ?? "" };
                for (var i = 0; i < post.Media.Length; i++)
                {
                    var photo = await Post($"{baseUrl}/photos", new() { ["url"] = Url(post.Media[i]), ["published"] = "false" });
                    form[$"attached_media[{i}]"] = JsonSerializer.Serialize(new { media_fbid = photo.GetProperty("id").GetString() });
                }
                return Id(await Post($"{baseUrl}/feed", form));
            }
            case "video":
                return Id(await Post($"{baseUrl}/videos", new() { ["file_url"] = Url(post.Media[0]), ["description"] = post.Caption ?? "" }));
            default:
                throw new InvalidOperationException($"Unknown post kind '{post.Kind}'.");
        }
    }

    /// <summary>Public, secret-free status the agency reads to mark items as published.</summary>
    public async Task<object> StatusAsync(CancellationToken ct)
    {
        var rows = await db.Set<MarketingPost>().AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new { x.Key, x.Channel, x.PublishedAtUtc, x.ExternalId, x.LastError, x.Attempts }).ToListAsync(ct);
        return new
        {
            enabled = meta.Value.Enabled,
            queued = ReadQueue().Select(p => new { p.Key, p.Kind, p.PublishAt }),
            posts = rows,
        };
    }
}
