namespace Dose.Domain.Sessions;

/// <summary>
/// A session held in Dose's own video room (LiveKit) instead of an outside meeting link. The admin picks it by typing
/// "dose" in the meeting-link field; the session then stores <see cref="Marker"/> as its link, so every existing
/// "has a link / can join" check keeps working, and joining opens /room/{booking token} on the site.
/// </summary>
public static class DoseRoom
{
    public const string Marker = "dose:room";
    public const string Provider = "Dose";

    public static bool IsRequested(string? input) =>
        input?.Trim().ToLowerInvariant() is "dose" or Marker or "دوز" or "dose room";

    public static bool Is(string? meetingUrl) => meetingUrl == Marker;

    /// <summary>LiveKit room name for a session.</summary>
    public static string RoomName(Guid sessionId) => "s-" + sessionId.ToString("N");
}
