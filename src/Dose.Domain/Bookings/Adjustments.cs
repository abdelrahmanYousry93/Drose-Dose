using Dose.Domain.Common;

namespace Dose.Domain.Bookings;

public enum AdjustmentType
{
    /// <summary>Group did not fill: the student owes the difference to the actual tier price.</summary>
    TopUpDue = 0,
    /// <summary>Dose waived a top-up.</summary>
    Waiver = 1,
    /// <summary>Money returned to the student.</summary>
    Refund = 2,
}

public enum AdjustmentStatus
{
    Pending = 0,
    /// <summary>Top-up paid and approved.</summary>
    Settled = 1,
    /// <summary>No longer due (group refilled, student chose refund/transfer, or waived).</summary>
    Cancelled = 2,
    /// <summary>Refund or waiver carried out.</summary>
    Completed = 3,
}

public enum TopUpChoice
{
    PayDifference = 0,
    Transfer = 1,
    Refund = 2,
}

/// <summary>
/// Financial ledger entry linked to a booking (spec 6.4/6.5). The amount is never edited — a changed amount
/// means cancelling this entry and recording a new one. The confirmed price snapshot is never modified.
/// </summary>
public sealed class FinancialAdjustment : Entity
{
    public Guid BookingId { get; init; }
    public Booking? Booking { get; set; }
    public AdjustmentType Type { get; init; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.Pending;
    public TopUpChoice? StudentChoice { get; set; }
    public DateTime? ChoiceAtUtc { get; set; }
    public DateTime? RespondByUtc { get; init; }
    public string? Reason { get; init; }
    public required string CreatedBy { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
}

public enum SessionRequestStatus
{
    New = 0,
    Contacted = 1,
    Fulfilled = 2,
    Closed = 3,
}

/// <summary>A student asks Dose to arrange a session when none fits (spec 7, "Session requests").</summary>
public sealed class SessionRequest : Entity
{
    public required string Reference { get; init; }
    public required string StudentName { get; init; }
    public required string ContactPhoneE164 { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhoneE164 { get; init; }
    public Guid GradeId { get; init; }
    public Guid StudyTrackId { get; init; }
    public Guid SubjectId { get; init; }
    public Guid ServiceTypeId { get; init; }
    public required string ScopeText { get; init; }
    public required string PreferredDays { get; init; }
    public required string PreferredTimes { get; init; }
    public Sessions.SessionMode Mode { get; init; }
    public Guid? PreferredTierId { get; init; }
    public string? Notes { get; init; }
    public SessionRequestStatus Status { get; set; } = SessionRequestStatus.New;
    public string? AdminNotes { get; set; }
    public Guid? FulfilledBySessionId { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; set; }
}
