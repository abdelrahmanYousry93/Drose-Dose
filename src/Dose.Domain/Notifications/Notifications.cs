using Dose.Domain.Common;

namespace Dose.Domain.Notifications;

/// <summary>Stable template codes. The texts themselves are data, editable by admins in both languages.</summary>
public static class TemplateCodes
{
    public const string BookingConfirmed = "booking_confirmed";
    public const string PaymentRejected = "payment_rejected";
    public const string SessionReminder = "session_reminder";
    public const string TopUpRequired = "topup_required";
    public const string RefundCompleted = "refund_completed";
    public const string SessionCancelled = "session_cancelled";
    public const string RequestReceived = "request_received";
}

/// <summary>An editable WhatsApp message text with placeholders like {student_name} and {link}.</summary>
public sealed class MessageTemplate
{
    public required string Code { get; init; }
    public required string TitleAr { get; set; }
    public required string TitleEn { get; set; }
    public required string BodyAr { get; set; }
    public required string BodyEn { get; set; }
    public bool IsActive { get; set; } = true;
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public enum NotificationStatus
{
    /// <summary>Waiting for someone on the team to send it (manual WhatsApp).</summary>
    Pending = 0,
    /// <summary>A team member confirmed it was sent.</summary>
    Sent = 1,
    /// <summary>Deliberately not sent.</summary>
    Skipped = 2,
}

/// <summary>
/// Outbox/log entry for one message to one recipient (spec 10). In the MVP a person sends it on WhatsApp and marks it
/// sent; a future WhatsApp Cloud API provider would send the same entries automatically. Never marked sent without
/// a confirmation. The booking link is not stored in the text: "{link}" is resolved only when the message is opened.
/// </summary>
public sealed class Notification : Entity
{
    public required string TemplateCode { get; init; }
    public Guid? BookingId { get; init; }
    public Guid? SessionRequestId { get; init; }
    public required string RecipientName { get; init; }
    public required string RecipientPhoneE164 { get; init; }
    public string? GuardianPhoneE164 { get; init; }
    public required string Language { get; init; }
    public required string Body { get; init; }
    /// <summary>Unique key preventing the same message from being queued twice for the same event.</summary>
    public required string DedupKey { get; init; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime DueAtUtc { get; init; }
    public DateTime? HandledAtUtc { get; set; }
    public string? HandledBy { get; set; }
}
