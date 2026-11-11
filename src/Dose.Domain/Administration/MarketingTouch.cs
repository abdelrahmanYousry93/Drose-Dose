using Dose.Domain.Common;

namespace Dose.Domain.Administration;

/// <summary>
/// One step of a visitor who arrived from a marketing post (?src=fb-w01-03): the visit, a WhatsApp click, a booking
/// hold or a session request. Holds and requests keep their public reference so paid bookings can be counted later.
/// No personal data: only the post code, the step and the public reference.
/// </summary>
public sealed class MarketingTouch : Entity
{
    private MarketingTouch() { }

    public MarketingTouch(string source, string step, string? reference, DateTime nowUtc)
    {
        Source = source;
        Step = step;
        Reference = reference;
        CreatedAtUtc = nowUtc;
    }

    public string Source { get; private set; } = "";
    /// <summary>visit | whatsapp | hold | request</summary>
    public string Step { get; private set; } = "";
    public string? Reference { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static readonly string[] Steps = ["visit", "whatsapp", "hold", "request"];
}
