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
    /// <summary>Optional: the Instagram business account id; read from the Page when empty.</summary>
    public string? InstagramUserId { get; init; }
    public int InstagramPollSeconds { get; init; } = 5;
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
    string? EditOf = null, string? Title = null, string[]? InstagramMedia = null);

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
            .Where(p => p.PublishAt.UtcDateTime <= now && p.PublishAt.UtcDateTime > now.AddDays(-2))
            .OrderBy(p => p.PublishAt).ToList();
        foreach (var post in due)
        foreach (var channel in (post.Channels ?? ["facebook"]).Where(c => c is "facebook" or "instagram").Distinct())
        {
            // Edits and card refreshes only exist on Facebook.
            if (channel == "instagram" && post.Kind is "edit" or "rescrape") continue;
            var row = await db.Set<MarketingPost>().FirstOrDefaultAsync(x => x.Key == post.Key && x.Channel == channel, ct);
            if (row is { PublishedAtUtc: not null } || row?.Attempts >= 3) continue;
            if (row is null) { row = new MarketingPost(post.Key, channel, now); db.Add(row); }
            try
            {
                var id = channel == "instagram" ? await PublishToInstagramAsync(post, ct) : await PublishToPageAsync(post, ct);
                row.Published(id, DateTime.UtcNow);
                log.LogInformation("Published marketing post {Key} to {Channel}", post.Key, channel);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                row.Failed(ex.Message);
                log.LogError(ex, "Publishing marketing post {Key} to {Channel} failed", post.Key, channel);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Instagram (the business account linked to the Page) through the Content Publishing API: create a media container,
    /// wait until Instagram has fetched it, publish. Captions keep "link in bio" (Instagram captions can't hold links).
    /// </summary>
    async Task<string> PublishToInstagramAsync(QueuedPost post, CancellationToken ct)
    {
        var m = meta.Value;
        var client = http.CreateClient("meta");
        var token = await PageTokenAsync(ct);
        var graph = $"https://graph.facebook.com/{m.GraphVersion}";

        async Task<JsonElement> Call(HttpMethod method, string url, Dictionary<string, string>? form = null)
        {
            form ??= [];
            form["access_token"] = token;
            using var req = method == HttpMethod.Get
                ? new HttpRequestMessage(method, $"{url}{(url.Contains('?') ? '&' : '?')}{string.Join('&', form.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))}")
                : new HttpRequestMessage(method, url) { Content = new FormUrlEncodedContent(form) };
            using var res = await client.SendAsync(req, ct);
            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct) is { Length: > 0 } b ? b : "{}").RootElement.Clone();
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(json.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var msg)
                    ? $"Instagram {(int)res.StatusCode}: {msg.GetString()}" : $"Instagram {(int)res.StatusCode}");
            return json;
        }

        var igUser = m.InstagramUserId;
        if (string.IsNullOrWhiteSpace(igUser))
        {
            var page = await Call(HttpMethod.Get, $"{graph}/{m.PageId}", new() { ["fields"] = "instagram_business_account" });
            igUser = page.TryGetProperty("instagram_business_account", out var ig) ? ig.GetProperty("id").GetString() : null;
            if (string.IsNullOrWhiteSpace(igUser)) throw new InvalidOperationException("No Instagram business account is linked to the Page.");
        }

        async Task WaitReady(string container)
        {
            for (var i = 0; i < 30; i++)
            {
                var st = await Call(HttpMethod.Get, $"{graph}/{container}", new() { ["fields"] = "status_code" });
                var code = st.TryGetProperty("status_code", out var c) ? c.GetString() : "FINISHED";
                if (code == "FINISHED") return;
                if (code is "ERROR" or "EXPIRED") throw new InvalidOperationException($"Instagram could not process the media ({code}).");
                await Task.Delay(TimeSpan.FromSeconds(m.InstagramPollSeconds), ct);
            }
            throw new InvalidOperationException("Instagram took too long to process the media.");
        }

        var caption = post.Caption ?? "";
        var media = post.InstagramMedia is { Length: > 0 } im ? im : post.Media;
        string container;
        switch (post.Kind)
        {
            case "photo" or "link":
                container = (await Call(HttpMethod.Post, $"{graph}/{igUser}/media",
                    new() { ["image_url"] = MediaUrl(media[0]), ["caption"] = caption })).GetProperty("id").GetString()!;
                break;
            case "album":
            {
                var children = new List<string>();
                foreach (var item in media.Take(10))
                    children.Add((await Call(HttpMethod.Post, $"{graph}/{igUser}/media",
                        new() { ["image_url"] = MediaUrl(item), ["is_carousel_item"] = "true" })).GetProperty("id").GetString()!);
                foreach (var child in children) await WaitReady(child);
                container = (await Call(HttpMethod.Post, $"{graph}/{igUser}/media",
                    new() { ["media_type"] = "CAROUSEL", ["children"] = string.Join(',', children), ["caption"] = caption })).GetProperty("id").GetString()!;
                break;
            }
            case "video":
                container = (await Call(HttpMethod.Post, $"{graph}/{igUser}/media",
                    new() { ["media_type"] = "REELS", ["video_url"] = MediaUrl(media[0]), ["caption"] = caption, ["share_to_feed"] = "true" }))
                    .GetProperty("id").GetString()!;
                break;
            default:
                throw new InvalidOperationException($"Post kind '{post.Kind}' can't go to Instagram.");
        }
        await WaitReady(container);
        return (await Call(HttpMethod.Post, $"{graph}/{igUser}/media_publish", new() { ["creation_id"] = container }))
            .GetProperty("id").GetString()!;
    }

    async Task<string> PublishToPageAsync(QueuedPost post, CancellationToken ct)
    {
        var m = meta.Value;
        var baseUrl = $"https://graph.facebook.com/{m.GraphVersion}/{m.PageId}";
        string Url(string media) => MediaUrl(media);
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
            {
                // Page Reels API (needs only pages_manage_posts): start an upload, let Facebook fetch the file, publish.
                var start = await Post($"{baseUrl}/video_reels", new() { ["upload_phase"] = "start" });
                var videoId = start.GetProperty("video_id").GetString()!;
                using (var upload = new HttpRequestMessage(HttpMethod.Post, $"https://rupload.facebook.com/video-upload/{m.GraphVersion}/{videoId}"))
                {
                    upload.Headers.TryAddWithoutValidation("Authorization", $"OAuth {token}");
                    upload.Headers.TryAddWithoutValidation("file_url", Url(post.Media[0]));
                    using var res = await client.SendAsync(upload, ct);
                    if (!res.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Meta upload {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync(ct)}");
                }
                var done = await Post($"{baseUrl}/video_reels", new()
                {
                    ["upload_phase"] = "finish", ["video_id"] = videoId, ["video_state"] = "PUBLISHED", ["description"] = caption,
                });
                return done.TryGetProperty("post_id", out var pid) && pid.GetString() is { Length: > 0 } p ? p : videoId;
            }
            case "link":
                // Make sure Facebook has read the card (image + title) before the post goes out.
                await Post($"https://graph.facebook.com/{m.GraphVersion}/", new() { ["id"] = SharePageUrl(post.Key), ["scrape"] = "true" });
                // A link post: Facebook shows the share page's image as a big card; tapping it opens the site.
                return Id(await Post($"{baseUrl}/feed", new() { ["message"] = caption, ["link"] = SharePageUrl(post.Key) }));
            case "rescrape":
                // Ask Facebook to read a link card again (after its share page changed); existing posts show the new card.
                await Post($"https://graph.facebook.com/{m.GraphVersion}/", new() { ["id"] = SharePageUrl(post.EditOf!), ["scrape"] = "true" });
                return $"rescrape:{post.EditOf}";
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

    /// <summary>Media deployed with the site are served by it; newer ones come straight from the repo.</summary>
    public string MediaUrl(string media)
    {
        var m = meta.Value;
        if (media.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return media;
        var rel = media.TrimStart('/');
        var deployed = env.WebRootPath is { } root && File.Exists(Path.Combine(root, rel));
        return deployed || string.IsNullOrWhiteSpace(m.MediaBaseUrl)
            ? $"{site.Value.AppBaseUrl.TrimEnd('/')}/{rel}" : $"{m.MediaBaseUrl.TrimEnd('/')}/{rel}";
    }

    /// <summary>The share page of a "link" post: its image and title become the clickable card on Facebook.</summary>
    public string SharePageUrl(string key) => $"{site.Value.AppBaseUrl.TrimEnd('/')}/p/{Uri.EscapeDataString(key)}";

    /// <summary>Where a click on a post lands: the home page, tagged with the post.</summary>
    public string LandingUrl(string key) => $"{site.Value.AppBaseUrl.TrimEnd('/')}/?src=fb-{Uri.EscapeDataString(key)}";

    /// <summary>
    /// Small HTML page for /p/{key}: Open Graph tags (image, title, description) that Facebook turns into a big clickable
    /// card, then an immediate redirect of real visitors to the site. Null when the key isn't in the schedule.
    /// </summary>
    public async Task<string?> SharePageAsync(string key, CancellationToken ct)
    {
        var post = (await ReadQueueAsync(ct)).FirstOrDefault(p => p.Key == key && p.Kind == "link");
        if (post is null || post.Media.Length == 0) return null;
        static string H(string? v) => System.Net.WebUtility.HtmlEncode(v ?? "");
        var title = post.Title ?? "دروس Dose — احجز شرح للدرس اللي واقف معاك بس";
        var description = (post.Caption ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var landing = LandingUrl(key);
        return $"""
            <!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8">
            <title>{H(title)}</title>
            <meta property="og:type" content="website">
            <meta property="og:site_name" content="دروس Dose">
            <meta property="og:title" content="{H(title)}">
            <meta property="og:description" content="{H(description)}">
            <meta property="og:image" content="{H(MediaUrl(post.Media[0]))}">
            <meta property="og:image:width" content="1200"><meta property="og:image:height" content="630">
            <meta property="og:url" content="{H(SharePageUrl(key))}">
            </head><body><a href="{H(landing)}">{H(title)}</a></body></html>
            """;
    }

    /// <summary>
    /// Adds the booking link (site URL + ?src=fb-{key}) at the end of the caption unless the caption already has a link.
    /// Instagram-style "link in bio" lines are replaced, since Facebook captions can hold a clickable link.
    /// </summary>
    public string WithLink(QueuedPost post)
    {
        var caption = (post.Caption ?? "").Replace("اللينك في البايو", "اللينك تحت").Trim();
        if (post.Kind == "link") return caption.Replace("اللينك تحت", "اضغط على الصورة");
        if (caption.Contains("http", StringComparison.OrdinalIgnoreCase)) return caption;
        var link = LandingUrl(post.Key);
        return $"{caption}\n\n👈 احجز الدرس اللي محتاجه من هنا:\n{link}".Trim();
    }

    /// <summary>
    /// Posting needs the Page's own token. When the configured key is the owner's (user) token, the Page token is read
    /// from it (GET /{page-id}?fields=access_token); a Page token is used as is.
    /// </summary>
    public Task<string> PageTokenForReadAsync(CancellationToken ct) => PageTokenAsync(ct);

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
            // Instagram: can the key read the linked business account (needs instagram_basic)?
            string? instagram = null;
            try
            {
                var pageToken = await PageTokenAsync(ct);
                using var ig = await http.CreateClient("meta").GetAsync(
                    $"https://graph.facebook.com/{m.GraphVersion}/{m.PageId}?fields=instagram_business_account%7Busername%7D&access_token={Uri.EscapeDataString(pageToken)}", ct);
                var igJson = JsonDocument.Parse(await ig.Content.ReadAsStringAsync(ct)).RootElement;
                instagram = ig.IsSuccessStatusCode
                    ? igJson.TryGetProperty("instagram_business_account", out var acc) && acc.TryGetProperty("username", out var u) ? u.GetString() : "not linked"
                    : igJson.TryGetProperty("error", out var ie) && ie.TryGetProperty("message", out var im) ? $"error: {im.GetString()}" : "error";
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { instagram = $"error: {ex.Message}"; }
            result = res.IsSuccessStatusCode
                ? new { ok = true, name = json.GetProperty("name").GetString(), keyType, instagram }
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
