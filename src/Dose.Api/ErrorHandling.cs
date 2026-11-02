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
