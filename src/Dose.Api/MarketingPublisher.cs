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
    /// <summary>
    /// Where the live schedule is read from (every run, no deploy needed): the public repo's main branch. The local
    /// QueuePath copy is only a fallback when GitHub is unreachable. Empty disables the remote read (tests, local dev).
    /// </summary>
    public string QueueUrl { get; init; } =
        "https://raw.githubusercontent.com/abdelrahmanYousry93/Drose-Dose/main/marketing/social-queue.json";
    /// <summary>Base for media not deployed with the site yet (/m/... paths in the repo's public folder).</summary>
    public string MediaBaseUrl { get; init; } =
        "https://raw.githubusercontent.com/abdelrahmanYousry93/Drose-Dose/main/src/Dose.Web/public";
    public bool Enabled => !string.IsNullOrWhiteSpace(PageId) && !string.IsNullOrWhiteSpace(PageAccessToken);
}

/// <summary>An approved post waiting for its time. Media are site-relative paths under /m/ (public JPEG/MP4 files).</summary>
public sealed record QueuedPost(
    string Key, string Kind, string? Caption, string[] Media, DateTimeOffset PublishAt, string[]? Channels,
    string? EditOf = null);

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

    static (DateTime At, List<QueuedPost> Posts)? remoteQueue;

    /// <summary>The schedule from GitHub (cached 2 minutes), falling back to the copy shipped with the site.</summary>
    public async Task<IReadOnlyList<QueuedPost>> ReadQueueAsync(CancellationToken ct)
    {
        var url = meta.Value.QueueUrl;
        if (string.IsNullOrWhiteSpace(url)) return ReadQueue();
        if (remoteQueue is { } c && c.At > DateTime.UtcNow.AddMinutes(-2)) return c.Posts;
        try
        {
            var text = await http.CreateClient("meta").GetStringAsync($"{url}?t={DateTime.UtcNow.Ticks}", ct);
            var posts = JsonSerializer.Deserialize<List<QueuedPost>>(text, Json) ?? [];
            remoteQueue = (DateTime.UtcNow, posts);
            return posts;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Could not read the marketing schedule from {Url}; using the deployed copy", url);
            return ReadQueue();
        }
    }

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
        var due = (await ReadQueueAsync(ct))
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
        // Media deployed with the site are served by it; newer ones come straight from the repo.
        string Url(string media)
        {
            if (media.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return media;
            var rel = media.TrimStart('/');
            var deployed = env.WebRootPath is { } root && File.Exists(Path.Combine(root, rel));
            return deployed || string.IsNullOrWhiteSpace(m.MediaBaseUrl)
                ? $"{site.Value.AppBaseUrl.TrimEnd('/')}/{rel}" : $"{m.MediaBaseUrl.TrimEnd('/')}/{rel}";
        }
        var client = http.CreateClient("meta");
        var token = await PageTokenAsync(ct);

        async Task<JsonElement> Post(string url, Dictionary<string, string> form)
        {
            form["access_token"] = token;
            using var res = await client.PostAsync(url, new FormUrlEncodedContent(form), ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body).RootElement.Clone();
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(json.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var msg)
                    ? $"Meta {(int)res.StatusCode}: {msg.GetString()}" : $"Meta {(int)res.StatusCode}");
            return json;
        }
        static string Id(JsonElement j) => j.TryGetProperty("post_id", out var p) ? p.GetString()! : j.GetProperty("id").GetString()!;

        // Every Facebook post carries a clickable link to the site, tagged with the post so visits can be traced back.
        var caption = WithLink(post);

        switch (post.Kind)
        {
            case "photo":
                return Id(await Post($"{baseUrl}/photos", new() { ["url"] = Url(post.Media[0]), ["message"] = caption }));
            case "album":
            {
                var form = new Dictionary<string, string> { ["message"] = caption };
                for (var i = 0; i < post.Media.Length; i++)
                {
                    var photo = await Post($"{baseUrl}/photos", new() { ["url"] = Url(post.Media[i]), ["published"] = "false" });
                    form[$"attached_media[{i}]"] = JsonSerializer.Serialize(new { media_fbid = photo.GetProperty("id").GetString() });
                }
                return Id(await Post($"{baseUrl}/feed", form));
            }
            case "video":
                return Id(await Post($"{baseUrl}/videos", new() { ["file_url"] = Url(post.Media[0]), ["description"] = caption }));
            case "edit":
            {
                // Replace the text of a post this publisher already made (EditOf = that post's queue key).
                var target = await db.Set<MarketingPost>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Key == post.EditOf && x.Channel == "facebook" && x.ExternalId != null, ct)
                    ?? throw new InvalidOperationException($"No published post '{post.EditOf}' to edit.");
                var text = WithLink(post with { Key = post.EditOf! });
                var res = await Post($"https://graph.facebook.com/{m.GraphVersion}/{target.ExternalId}", new() { ["message"] = text });
                return target.ExternalId!;
            }
            default:
                throw new InvalidOperationException($"Unknown post kind '{post.Kind}'.");
        }
    }

    static (DateTime At, object Result)? pageCheck;

    /// <summary>
    /// Adds the booking link (site URL + ?src=fb-{key}) at the end of the caption unless the caption already has a link.
    /// Instagram-style "link in bio" lines are replaced, since Facebook captions can hold a clickable link.
    /// </summary>
    public string WithLink(QueuedPost post)
    {
        var caption = (post.Caption ?? "").Replace("اللينك في البايو", "اللينك تحت").Trim();
        if (caption.Contains("http", StringComparison.OrdinalIgnoreCase)) return caption;
        var link = $"{site.Value.AppBaseUrl.TrimEnd('/')}/?src=fb-{Uri.EscapeDataString(post.Key)}";
        return $"{caption}\n\n👈 احجز الدرس اللي محتاجه من هنا:\n{link}".Trim();
    }

    /// <summary>
    /// Posting needs the Page's own token. When the configured key is the owner's (user) token, the Page token is read
    /// from it (GET /{page-id}?fields=access_token); a Page token is used as is.
    /// </summary>
    async Task<string> PageTokenAsync(CancellationToken ct)
    {
        var m = meta.Value;
        try
        {
            using var res = await http.CreateClient("meta").GetAsync(
                $"https://graph.facebook.com/{m.GraphVersion}/{m.PageId}?fields=access_token&access_token={Uri.EscapeDataString(m.PageAccessToken!)}", ct);
            if (res.IsSuccessStatusCode
                && JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement.TryGetProperty("access_token", out var t)
                && t.GetString() is { Length: > 0 } pageToken)
                return pageToken;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Could not read the Page token; using the configured key"); }
        return m.PageAccessToken!;
    }

    /// <summary>Reads the Page's name with the token (no posting) to prove the key works; cached for 10 minutes.</summary>
    async Task<object?> CheckPageAsync(CancellationToken ct)
    {
        if (!meta.Value.Enabled) return null;
        if (pageCheck is { } c && c.At > DateTime.UtcNow.AddMinutes(-10)) return c.Result;
        object result;
        try
        {
            var m = meta.Value;
            using var res = await http.CreateClient("meta").GetAsync(
                $"https://graph.facebook.com/{m.GraphVersion}/{m.PageId}?fields=name&access_token={Uri.EscapeDataString(m.PageAccessToken!)}", ct);
            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement;
            string? keyType = null;
            using (var me = await http.CreateClient("meta").GetAsync(
                $"https://graph.facebook.com/{m.GraphVersion}/me?fields=id&access_token={Uri.EscapeDataString(m.PageAccessToken!)}", ct))
                if (me.IsSuccessStatusCode && JsonDocument.Parse(await me.Content.ReadAsStringAsync(ct)).RootElement.TryGetProperty("id", out var id))
                    keyType = id.GetString() == m.PageId ? "page" : "user";
            result = res.IsSuccessStatusCode
                ? new { ok = true, name = json.GetProperty("name").GetString(), keyType }
                : new { ok = false, error = json.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var msg) ? msg.GetString() : $"HTTP {(int)res.StatusCode}" };
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { result = new { ok = false, error = ex.Message }; }
        pageCheck = (DateTime.UtcNow, result);
        return result;
    }

    /// <summary>Public, secret-free status the agency reads to mark items as published.</summary>
    public async Task<object> StatusAsync(CancellationToken ct)
    {
        var rows = await db.Set<MarketingPost>().AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new { x.Key, x.Channel, x.PublishedAtUtc, x.ExternalId, x.LastError, x.Attempts }).ToListAsync(ct);
        return new
        {
            enabled = meta.Value.Enabled,
            page = await CheckPageAsync(ct),
            queued = (await ReadQueueAsync(ct)).Select(p => new { p.Key, p.Kind, p.PublishAt }),
            posts = rows,
        };
    }
}
