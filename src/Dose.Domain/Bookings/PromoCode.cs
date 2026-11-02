using Dose.Domain.Common;

namespace Dose.Domain.Bookings;

public enum PromoKind
{
    Percent = 0,
    Fixed = 1,
}

/// <summary>
/// Discount code entered by the student at booking. Optional scope (grade, subject, service type, lesson, one session)
/// and limits (dates, total uses, uses per WhatsApp number, uses per session). A use counts while the booking is active
/// or paid; expired and cancelled-before-payment bookings free it again.
/// </summary>
public sealed class PromoCode : Entity
{
    /// <summary>Upper-case, A–Z/0–9/-, unique.</summary>
    public required string Code { get; set; }
    public string? Description { get; set; }
    public PromoKind Kind { get; set; }
    /// <summary>Percent (1–100) or amount in the booking currency.</summary>
    public decimal Value { get; set; }

    public Guid? GradeId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? ServiceTypeId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? SessionId { get; set; }

    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    public int? MaxTotalUses { get; set; }
    public int? MaxUsesPerPhone { get; set; }
    public int? MaxUsesPerSession { get; set; }
    public bool IsActive { get; set; } = true;
    public required string CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public static class PromoErrors
{
    public const string Invalid = "promo.invalid";
}
