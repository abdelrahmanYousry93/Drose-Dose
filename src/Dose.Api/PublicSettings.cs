namespace Dose.Api;

/// <summary>
/// Settings that are safe to expose to anonymous clients.
/// Values come from configuration (appsettings / environment), never from code.
/// </summary>
public sealed class PublicSettings
{
    public const string SectionName = "Public";

    /// <summary>Dose WhatsApp Business number in E.164 format, e.g. +2010xxxxxxxx.</summary>
    public string WhatsAppBusinessNumber { get; init; } = string.Empty;

    /// <summary>Public web address of the app, used to build booking links in messages (e.g. https://dose.example).</summary>
    public string AppBaseUrl { get; init; } = "http://localhost:4200";

    /// <summary>IANA business time zone used for display, e.g. Africa/Cairo.</summary>
    public string BusinessTimeZone { get; init; } = "Africa/Cairo";

    /// <summary>Meta pixel (dataset) id for ad measurement. Public by design; empty disables the pixel.</summary>
    public string MetaPixelId { get; init; } = string.Empty;

    /// <summary>TikTok pixel id for ad measurement. Public by design; empty disables the pixel.</summary>
    public string TikTokPixelId { get; init; } = string.Empty;
}
