using Dose.Application.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dose.Api;

/// <summary>Maps domain rule violations to RFC 7807 problem responses with a stable "code".</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problems, ILogger<DomainExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException ex) return false;

        var status = ex.Code switch
        {
            BookingErrors.NotFound or BookingErrors.SessionNotFound or "payment.proof_not_found" or "payment.method_not_found"
                or AdminErrors.NotFound
                => StatusCodes.Status404NotFound,
            AdminErrors.Conflict => StatusCodes.Status409Conflict,
            "payment.file_deleted" => StatusCodes.Status410Gone,
            Dose.Application.Teachers.TeacherErrors.NotLinked or Dose.Application.Teachers.TeacherErrors.NotYours => StatusCodes.Status403Forbidden,
            PricingErrors.NotConfigured or PricingErrors.Ambiguous => StatusCodes.Status422UnprocessableEntity,
            BookingErrors.SessionFull or BookingErrors.SessionClosed or BookingErrors.Duplicate or BookingErrors.Expired
                or BookingErrors.InvalidState or BookingErrors.PriceChanged or BookingErrors.JoinNotAvailable
                => StatusCodes.Status409Conflict,
            PricingErrors.TierNotFound or "payment.file_missing"
                => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest,
        };
        if (status >= 500 || status == 422) log.LogWarning(ex, "Configuration error {Code}", ex.Code);

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = ex.Text.For(HttpRequestLanguage.Resolve(http)), Extensions = { ["code"] = ex.Code } },
            Exception = ex,
        });
    }
}

/// <summary>
/// Malformed requests (a missing or empty required field, a value of the wrong type, broken JSON) get a clear,
/// localized message with a stable code instead of a bare "Bad Request".
/// </summary>
public sealed class BadRequestExceptionHandler(IProblemDetailsService problems, ILogger<BadRequestExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not BadHttpRequestException bad) return false;
        log.LogInformation("Rejected malformed request to {Path}: {Reason}", http.Request.Path, bad.Message);
        var status = bad.StatusCode is >= 400 and < 500 ? bad.StatusCode : StatusCodes.Status400BadRequest;
        var en = HttpRequestLanguage.Resolve(http) == "en";
        var (code, title) = status == StatusCodes.Status413PayloadTooLarge
            ? ("http.too_large", en ? "The file or data you sent is too large." : "الملف أو البيانات اللي بعتها حجمها كبير.")
            : ("http.invalid_input", en ? "Some required information is missing or not in the right format. Check the fields and try again."
                                        : "فيه بيانات مطلوبة ناقصة أو مكتوبة بشكل غلط. راجع الخانات وجرّب تاني.");
        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Extensions = { ["code"] = code } },
            Exception = exception,
        });
    }
}

/// <summary>
/// The database is unreachable (paused, limit reached, network): answer 503 with a clear, localized message and a
/// stable code instead of a generic server error, so the site can tell the visitor to try again shortly.
/// </summary>
public sealed class DatabaseUnavailableExceptionHandler(IProblemDetailsService problems, ILogger<DatabaseUnavailableExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (!IsDatabaseUnavailable(exception)) return false;
        log.LogError(exception, "Database unavailable for {Path}", http.Request.Path);
        var en = HttpRequestLanguage.Resolve(http) == "en";
        http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        http.Response.Headers.RetryAfter = "60";
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = en ? "Dose is briefly unavailable. Please try again in a few minutes."
                           : "دروس متوقفة مؤقتاً للصيانة. جرّب تاني بعد شوية.",
                Extensions = { ["code"] = "service.unavailable" },
            },
            Exception = exception,
        });
    }

    static bool IsDatabaseUnavailable(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is Microsoft.Data.SqlClient.SqlException sql
                && (sql.ClientConnectionId == Guid.Empty || sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(e => ConnectionErrors.Contains(e.Number))))
                return true; // never connected, or a connection-level error
        return false;
    }

    // Connection-level failures only (not constraint violations or other query errors):
    // timeout, network, cannot open database, Azure SQL unavailable / paused / resuming / throttled / firewall.
    static readonly HashSet<int> ConnectionErrors = [-2, 2, 53, 64, 233, 4060, 10053, 10054, 10060, 10928, 10929,
        40197, 40501, 40532, 40613, 40615, 42108, 42109, 49918, 49919, 49920];
}
