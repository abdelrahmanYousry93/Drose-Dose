using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Dose.Api;

/// <summary>
/// Production hardening that applies to every request: real client IP behind the Azure proxy, browser security
/// headers (CSP etc.), a same-origin check for state-changing API calls, a per-IP request ceiling and body limits.
/// </summary>
public static class Security
{
    public const string GlobalRateLimit = "global";

    /// <summary>
    /// Content Security Policy for the Angular app served from the same origin. Styles allow 'unsafe-inline' because
    /// Angular component styles are injected at runtime; scripts are same-origin only (no inline scripts are built).
    /// Images allow https: for teacher photo URLs entered by admins.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; " +
        "font-src 'self'; connect-src 'self'; manifest-src 'self'; worker-src 'self'; frame-src 'none'; " +
        "frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";

    public static void AddDoseSecurity(this WebApplicationBuilder builder)
    {
        // Azure App Service terminates TLS and forwards X-Forwarded-For / -Proto. Trust exactly one proxy hop.
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });

        builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; });
        // Behind App Service the public HTTPS port is 443 (the app itself listens on plain HTTP inside the container).
        builder.Services.AddHttpsRedirection(o => o.HttpsPort = builder.Configuration.GetValue("Security:HttpsPort", 443));

        // Largest legitimate body is a 5 MB screenshot (admin-configurable) inside a multipart form.
        builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 12 * 1024 * 1024);
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
        {
            o.MultipartBodyLengthLimit = 12 * 1024 * 1024;
            o.ValueCountLimit = 64;
        });
    }

    /// <summary>A ceiling per client IP across the whole API, on top of the stricter booking and login limits.</summary>
    public static void AddGlobalLimiter(this Microsoft.AspNetCore.RateLimiting.RateLimiterOptions o) =>
        o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            http.Request.Path.StartsWithSegments("/api")
                ? RateLimitPartition.GetSlidingWindowLimiter(ClientKey(http), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 300, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0,
                })
                : RateLimitPartition.GetNoLimiter("static"));

    public static string ClientKey(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static void UseDoseSecurity(this WebApplication app)
    {
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            // The platform health probe calls plain HTTP inside the network; everything else goes to HTTPS.
            app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api/health"), b => b.UseHttpsRedirection());
        }

        var allowedOrigins = AllowedOrigins(app);
        app.Use(async (http, next) =>
        {
            var headers = http.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.XFrameOptions = "DENY";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers.ContentSecurityPolicy = ContentSecurityPolicy;

            var isApi = http.Request.Path.StartsWithSegments("/api");
            if (isApi)
            {
                // Personal data must not sit in browser or proxy caches unless an endpoint opts in (teacher photos).
                http.Response.OnStarting(() =>
                {
                    if (!http.Response.Headers.ContainsKey("Cache-Control")) http.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                });

                // Defense in depth on top of the SameSite=Strict cookie: a state-changing call from a browser must come
                // from our own site. Requests without an Origin header (non-browser clients) are left to auth.
                if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) &&
                    !HttpMethods.IsOptions(http.Request.Method) &&
                    http.Request.Headers.Origin is { Count: > 0 } origin &&
                    !IsAllowedOrigin(origin.ToString(), http, allowedOrigins))
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await http.Response.WriteAsJsonAsync(new { title = "Cross-site request blocked.", status = 403, code = "http.cross_site" });
                    return;
                }
            }
            await next();
        });
    }

    private static HashSet<string> AllowedOrigins(WebApplication app)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var appBase = app.Services.GetRequiredService<IOptions<PublicSettings>>().Value.AppBaseUrl;
        if (Uri.TryCreate(appBase, UriKind.Absolute, out var u)) set.Add(u.GetLeftPart(UriPartial.Authority));
        foreach (var extra in app.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? [])
            if (Uri.TryCreate(extra, UriKind.Absolute, out var e)) set.Add(e.GetLeftPart(UriPartial.Authority));
        return set;
    }

    private static bool IsAllowedOrigin(string origin, HttpContext http, HashSet<string> allowed)
    {
        if (allowed.Contains(origin)) return true;
        var self = $"{http.Request.Scheme}://{http.Request.Host}";
        return string.Equals(origin, self, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Serves the built Angular app (wwwroot) when present, so the site and API share one origin and the SameSite
    /// cookie keeps working. Hashed bundles are cached for a year; index.html and the service-worker files never.
    /// </summary>
    public static void MapDoseSpa(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath;
        if (string.IsNullOrEmpty(webRoot) || !File.Exists(Path.Combine(webRoot, "index.html"))) return;

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var name = Path.GetFileName(ctx.File.Name);
                ctx.Context.Response.Headers.CacheControl =
                    name is "index.html" or "ngsw.json" or "ngsw-worker.js" or "safety-worker.js" or "manifest.webmanifest"
                        ? "no-cache"
                        : System.Text.RegularExpressions.Regex.IsMatch(name, @"-[A-Za-z0-9_]{8,}\.(js|css)$")
                            ? "public, max-age=31536000, immutable"
                            : "public, max-age=86400";
            },
        });
        app.Map("/api/{**rest}", () => Results.NotFound());
        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
        });
    }
}
