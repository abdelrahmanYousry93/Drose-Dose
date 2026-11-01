using Dose.Application.Abstractions;

namespace Dose.Api;

/// <summary>Resolves "ar" (default) or "en" from the request's Accept-Language header.</summary>
public sealed class HttpRequestLanguage(IHttpContextAccessor http) : IRequestLanguage
{
    public string Code { get; } = Resolve(http.HttpContext);

    public static string Resolve(HttpContext? ctx)
    {
        var header = ctx?.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(header)) return "ar";
        var first = header.Split(',')[0].Trim();
        return first.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
    }
}
