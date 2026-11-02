using Dose.Domain.Common;

namespace Dose.Domain.Teachers;

public sealed class Teacher : Entity
{
    /// <summary>Identity user id when the teacher has a login (Phase 4).</summary>
    public string? UserId { get; set; }
    public required string DisplayNameAr { get; set; }
    public string? DisplayNameEn { get; set; }
    /// <summary>External photo URL (https). An uploaded photo (PhotoFileKey) takes precedence.</summary>
    public string? PhotoUrl { get; set; }
    public string? PhotoFileKey { get; set; }
    public string? PhotoContentType { get; set; }
    public int PhotoVersion { get; set; }
    public string? BioAr { get; set; }
    public string? BioEn { get; set; }
    public string? QualificationsAr { get; set; }
    public string? QualificationsEn { get; set; }
    public int? YearsOfExperience { get; set; }
    public bool IsPublished { get; set; }
}

/// <summary>Subjects a teacher teaches — used to offer their free times when students book a session themselves.</summary>
public sealed class TeacherSubject : Entity
{
    public Guid TeacherId { get; init; }
    public Guid SubjectId { get; init; }
}
