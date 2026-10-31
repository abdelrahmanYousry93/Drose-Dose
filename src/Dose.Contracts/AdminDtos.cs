namespace Dose.Contracts.Admin;

// ---------- Catalog ----------
public sealed record StageAdminDto(Guid? Id, string NameAr, bool RequiresGuardianContact, int SortOrder, bool IsActive);
public sealed record LookupAdminDto(Guid? Id, string NameAr, int SortOrder, bool IsActive);
public sealed record PricingBandAdminDto(Guid? Id, string NameAr, int SortOrder);
public sealed record GradeAdminDto(
    Guid? Id, string NameAr, Guid StageId, Guid PricingBandId, string? CurriculumSystem, int SortOrder, bool IsActive,
    IReadOnlyList<Guid> SubjectIds);
public sealed record ChapterAdminDto(Guid? Id, string NameAr, Guid SubjectId, Guid GradeId, Guid? StudyTrackId, int SortOrder, bool IsActive);
public sealed record LessonAdminDto(Guid? Id, string NameAr, Guid ChapterId, int SortOrder, bool IsActive);
public sealed record ServiceTypeAdminDto(Guid Id, string Code, string NameAr, string? DescriptionAr, int SortOrder, bool IsActive);

// ---------- Teachers & payment methods ----------
public sealed record TeacherAdminDto(
    Guid? Id, string DisplayNameAr, string? PhotoUrl, string? BioAr, string? QualificationsAr, int? YearsOfExperience, bool IsPublished);
public sealed record PaymentMethodAdminDto(
    Guid? Id, string Code, string NameAr, string ReceivingAccount, string? InstructionsAr, int SortOrder, bool IsActive);

// ---------- Pricing ----------
public sealed record TierAdminDto(Guid? Id, string NameAr, int MinParticipants, int MaxParticipants, decimal PricePerStudent, int SortOrder);
public sealed record PricingVersionAdminDto(Guid Id, int VersionNo, decimal? PrivatePrice, bool IsLocked, IReadOnlyList<TierAdminDto> Tiers);
public sealed record PricingPlanAdminDto(
    Guid? Id, string Name, Guid ServiceTypeId, string Mode, Guid? PricingBandId, Guid? StudyTrackId, Guid? SubjectId,
    string Currency, string Policy, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc, bool IsActive,
    PricingVersionAdminDto? CurrentVersion);
public sealed record SavePricesRequest(decimal? PrivatePrice, IReadOnlyList<TierAdminDto> Tiers);

// ---------- Sessions ----------
public sealed record PricingPreviewRequest(Guid ServiceTypeId, Guid GradeId, Guid StudyTrackId, Guid SubjectId, string Mode);
public sealed record PricingPreviewDto(
    Guid PlanId, string PlanName, Guid VersionId, int VersionNo, string Currency, decimal? PrivatePrice, IReadOnlyList<TierAdminDto> Tiers);

public sealed record SessionSaveRequest(
    Guid ServiceTypeId, Guid GradeId, Guid StudyTrackId, Guid SubjectId, Guid? ChapterId, Guid? LessonId,
    string ScopeTitleAr, string? ScopeDetailsAr, Guid TeacherId, DateTime StartUtc, int DurationMinutes,
    string Mode, Guid? TargetTierId, string? MeetingUrl);

public sealed record SessionAdminDto(
    Guid Id, string Status, string ScopeTitleAr, string? ScopeDetailsAr,
    Guid ServiceTypeId, string ServiceTypeNameAr, Guid GradeId, string GradeNameAr, Guid StudyTrackId, string StudyTrackNameAr,
    Guid SubjectId, string SubjectNameAr, Guid? ChapterId, Guid? LessonId, Guid TeacherId, string TeacherNameAr,
    DateTime StartUtc, DateTime EndUtc, int DurationMinutes, string Mode, int Capacity, int MinParticipants,
    Guid? TargetTierId, string? TargetTierNameAr, decimal PricePerStudent, string Currency, string PlanName,
    DateTime BookingDeadlineUtc, DateTime? TierDecisionAtUtc, string? MeetingUrl,
    int ConfirmedCount, int OccupiedCount, int ActiveBookingCount);

public sealed record SessionBookingAdminDto(
    Guid Id, string Reference, string Status, string? StudentName, string? ContactPhone, string? GuardianName,
    string? GuardianPhone, decimal Price, string Currency, DateTime CreatedAtUtc, DateTime? ConfirmedAtUtc);

public sealed record ReissueLinkResponse(string Reference, string AccessToken, string? ContactPhone, string Message);
