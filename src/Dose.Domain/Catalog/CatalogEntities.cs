using Dose.Domain.Common;

namespace Dose.Domain.Catalog;

/// <summary>Primary / Preparatory / Secondary. Stage-level rules live here as data.</summary>
public sealed class EducationalStage : Entity
{
    public required string NameAr { get; set; }
    public bool RequiresGuardianContact { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Language/track such as عربي or لغات. Admin-managed.</summary>
public sealed class StudyTrack : Entity
{
    public required string NameAr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Grade grouping used only for pricing. Assignment to grades is data.</summary>
public sealed class PricingBand : Entity
{
    public required string NameAr { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Grade : Entity
{
    public required string NameAr { get; set; }
    public Guid StageId { get; set; }
    public EducationalStage? Stage { get; set; }
    public Guid PricingBandId { get; set; }
    public PricingBand? PricingBand { get; set; }
    /// <summary>Optional curriculum/system label for secondary grades (e.g. a baccalaureate track).</summary>
    public string? CurriculumSystem { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Subject : Entity
{
    public required string NameAr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Which subjects are taught in which grade (and optionally which track).</summary>
public sealed class GradeSubject : Entity
{
    public Guid GradeId { get; set; }
    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }
}

public sealed class Chapter : Entity
{
    public required string NameAr { get; set; }
    public Guid SubjectId { get; set; }
    public Guid GradeId { get; set; }
    public Guid? StudyTrackId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Lesson : Entity
{
    public required string NameAr { get; set; }
    public Guid ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One lesson / chapter package / general revision — records, not code branches.</summary>
public sealed class ServiceType : Entity
{
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public string? DescriptionAr { get; set; }
    public bool RequiresPackage { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
