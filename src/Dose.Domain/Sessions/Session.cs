using Dose.Domain.Catalog;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Teachers;

namespace Dose.Domain.Sessions;

public enum SessionMode
{
    Private = 0,
    Group = 1,
}

public enum SessionStatus
{
    Draft = 0,
    Published = 1,
    TopUpRequired = 2,
    Confirmed = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
}

public sealed class Session : Entity
{
    public Guid ServiceTypeId { get; set; }
    public ServiceType? ServiceType { get; set; }
    public Guid GradeId { get; set; }
    public Grade? Grade { get; set; }
    public Guid StudyTrackId { get; set; }
    public StudyTrack? StudyTrack { get; set; }
    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public Guid? ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public Guid? LessonId { get; set; }
    public Lesson? Lesson { get; set; }
    /// <summary>Human-readable scope, e.g. the lesson title or revision coverage.</summary>
    public required string ScopeTitleAr { get; set; }
    public string? ScopeTitleEn { get; set; }
    public string? ScopeDetailsAr { get; set; }
    public string? ScopeDetailsEn { get; set; }

    public Guid TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public SessionMode Mode { get; set; }
    public int Capacity { get; set; }
    public int MinParticipants { get; set; }

    /// <summary>Fixed at publish time so later pricing edits never affect this session.</summary>
    public Guid PricingPlanVersionId { get; set; }
    public PricingPlanVersion? PricingPlanVersion { get; set; }
    public Guid? TargetTierId { get; set; }
    public PricingTier? TargetTier { get; set; }
    public PricingPolicy Policy { get; set; }

    public DateTime BookingDeadlineUtc { get; set; }
    public DateTime? TierDecisionAtUtc { get; set; }
    /// <summary>Set once the tier decision ran for this group session (idempotency marker).</summary>
    public DateTime? TierDecidedAtUtc { get; set; }
    /// <summary>Confirmed participant count used at the tier decision.</summary>
    public int? DecisionConfirmedCount { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Draft;

    /// <summary>Meeting details are never returned by public endpoints.</summary>
    public string? MeetingProvider { get; set; }
    public string? MeetingUrl { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public bool IsOpenForBooking(DateTime nowUtc) =>
        Status == SessionStatus.Published && nowUtc < BookingDeadlineUtc && nowUtc < StartUtc;
}
