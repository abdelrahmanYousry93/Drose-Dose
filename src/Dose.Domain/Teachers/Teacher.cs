using Dose.Domain.Common;

namespace Dose.Domain.Teachers;

public sealed class Teacher : Entity
{
    /// <summary>Identity user id when the teacher has a login (Phase 4).</summary>
    public string? UserId { get; set; }
    public required string DisplayNameAr { get; set; }
    public string? PhotoUrl { get; set; }
    public string? BioAr { get; set; }
    public string? QualificationsAr { get; set; }
    public int? YearsOfExperience { get; set; }
    public bool IsPublished { get; set; }
}
