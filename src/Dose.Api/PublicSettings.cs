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

    /// <summary>IANA business time zone used for display, e.g. Africa/Cairo.</summary>
    public string BusinessTimeZone { get; init; } = "Africa/Cairo";
}
