using Dose.Domain.Common;

namespace Dose.Domain.Teachers;

public enum AvailabilityStatus
{
    Submitted = 0,
    Approved = 1,
    Rejected = 2,
    Withdrawn = 3,
}

/// <summary>A time window a teacher offers. Teachers submit; admins approve (decision D11).</summary>
public sealed class TeacherAvailability : Entity
{
    public Guid TeacherId { get; init; }
    public Teacher? Teacher { get; set; }
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string? Note { get; init; }
    public AvailabilityStatus Status { get; set; } = AvailabilityStatus.Submitted;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAtUtc { get; init; }
}

public enum AttendanceStatus
{
    Attended = 0,
    Absent = 1,
}

/// <summary>Per-booking attendance recorded by the session's teacher (or an admin).</summary>
public sealed class Attendance : Entity
{
    public Guid BookingId { get; init; }
    public AttendanceStatus Status { get; set; }
    public required string MarkedBy { get; set; }
    public DateTime MarkedAtUtc { get; set; }
}
