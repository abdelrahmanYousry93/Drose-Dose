namespace Dose.Contracts.Admin;

// ---------- Catalog ----------
public sealed record StageAdminDto(Guid? Id, string NameAr, bool RequiresGuardianContact, int SortOrder, bool IsActive, string? NameEn = null);
public sealed record LookupAdminDto(Guid? Id, string NameAr, int SortOrder, bool IsActive, string? NameEn = null);
public sealed record PricingBandAdminDto(Guid? Id, string NameAr, int SortOrder, string? NameEn = null);
public sealed record GradeAdminDto(
    Guid? Id, string NameAr, Guid StageId, Guid PricingBandId, string? CurriculumSystem, int SortOrder, bool IsActive,
    IReadOnlyList<Guid> SubjectIds, string? NameEn = null);
public sealed record ChapterAdminDto(Guid? Id, string NameAr, Guid SubjectId, Guid GradeId, Guid? StudyTrackId, int SortOrder, bool IsActive, string? NameEn = null);
public sealed record LessonAdminDto(Guid? Id, string NameAr, Guid ChapterId, int SortOrder, bool IsActive, string? NameEn = null);
public sealed record ServiceTypeAdminDto(Guid Id, string Code, string NameAr, string? DescriptionAr, int SortOrder, bool IsActive, string? NameEn = null, string? DescriptionEn = null, int DefaultDurationMinutes = 60);

// ---------- Teachers & payment methods ----------
public sealed record TeacherAdminDto(
    Guid? Id, string DisplayNameAr, string? PhotoUrl, string? BioAr, string? QualificationsAr, int? YearsOfExperience, bool IsPublished, string? DisplayNameEn = null, string? BioEn = null, string? QualificationsEn = null, bool HasUploadedPhoto = false, string? PhotoPreviewUrl = null, IReadOnlyList<Guid>? SubjectIds = null);
public sealed record PaymentMethodAdminDto(
    Guid? Id, string Code, string NameAr, string ReceivingAccount, string? InstructionsAr, int SortOrder, bool IsActive, string? NameEn = null, string? InstructionsEn = null);

// ---------- Pricing ----------
public sealed record TierAdminDto(Guid? Id, string NameAr, int MinParticipants, int MaxParticipants, decimal PricePerStudent, int SortOrder, string? NameEn = null);
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
    string Mode, Guid? TargetTierId, string? MeetingUrl, string? ScopeTitleEn = null, string? ScopeDetailsEn = null);

public sealed record SessionAdminDto(
    Guid Id, string Status, string ScopeTitleAr, string? ScopeDetailsAr,
    Guid ServiceTypeId, string ServiceTypeNameAr, Guid GradeId, string GradeNameAr, Guid StudyTrackId, string StudyTrackNameAr,
    Guid SubjectId, string SubjectNameAr, Guid? ChapterId, Guid? LessonId, Guid TeacherId, string TeacherNameAr,
    DateTime StartUtc, DateTime EndUtc, int DurationMinutes, string Mode, int Capacity, int MinParticipants,
    Guid? TargetTierId, string? TargetTierNameAr, decimal PricePerStudent, string Currency, string PlanName,
    DateTime BookingDeadlineUtc, DateTime? TierDecisionAtUtc, string? MeetingUrl,
    int ConfirmedCount, int OccupiedCount, int ActiveBookingCount, string? ScopeTitleEn = null, string? ScopeDetailsEn = null,
    bool IsClosed = false, string? ProposedByTeacherName = null, string? ReviewNote = null);

public sealed record SessionBookingAdminDto(
    Guid Id, string Reference, string Status, string? StudentName, string? ContactPhone, string? GuardianName,
    string? GuardianPhone, decimal Price, string Currency, DateTime CreatedAtUtc, DateTime? ConfirmedAtUtc);

public sealed record ReissueLinkResponse(string Reference, string AccessToken, string? ContactPhone, string Message);

// ---------- Follow-ups (top-ups, refunds, transfers) ----------
public sealed record FollowUpDto(
    Guid BookingId, string Kind, string BookingReference, string BookingStatus, string? StudentName, string? ContactPhone,
    string? GuardianPhone, Guid SessionId, string SessionTitleAr, DateTime SessionStartUtc, decimal PaidAmount, string Currency,
    decimal? TopUpAmount, DateTime? RespondByUtc, string? StudentChoice, string? TopUpProofStatus, string? Reason, bool Overdue,
    string Language = "ar", string? SessionTitleEn = null);
public sealed record ResolveRequest(string? Note);

// ---------- Session requests ----------
public sealed record SessionRequestAdminDto(
    Guid Id, string Reference, string Status, string StudentName, string ContactPhone, string? GuardianName, string? GuardianPhone,
    Guid GradeId, string GradeNameAr, Guid StudyTrackId, string StudyTrackNameAr, Guid SubjectId, string SubjectNameAr,
    Guid ServiceTypeId, string ServiceTypeNameAr, string ScopeText, string PreferredDays, string PreferredTimes, string Mode,
    string? PreferredTierNameAr, string? Notes, string? AdminNotes, Guid? FulfilledBySessionId, DateTime CreatedAtUtc,
    int? GroupSize = null);
/// <summary>Admin offers a time for a private session / private group request: creates the session and the student's payment link.</summary>
public sealed record OfferRequest(Guid TeacherId, DateTime StartUtc, int? DurationMinutes, string? Title);
public sealed record OfferResponse(Guid SessionId, string BookingReference);
public sealed record UpdateRequestStatus(string Status, string? AdminNotes, Guid? FulfilledBySessionId);

// ---------- Dashboard ----------
public sealed record DashboardSessionDto(
    Guid Id, string TitleAr, string SubjectNameAr, string TeacherNameAr, DateTime StartUtc, string Mode, string Status,
    int ConfirmedCount, int Capacity, int MinParticipants, bool HasMeetingUrl, DateTime? TierDecisionAtUtc);
public sealed record AdminDashboardDto(
    int PendingProofs, double? OldestPendingProofMinutes, int OpenTopUps, int OverdueTopUps, int PendingRefunds,
    int PendingTransfers, int NewRequests, int PendingAvailability,
    IReadOnlyList<DashboardSessionDto> UpcomingSessions, IReadOnlyList<DashboardSessionDto> GroupsNearDecision,
    int PeriodDays, int PeriodConfirmedBookings, decimal PeriodCollected, decimal PeriodRefunded, string Currency, int PendingMessages = 0);

// ---------- Users ----------
public sealed record UserAdminDto(string Id, string Email, IReadOnlyList<string> Roles, bool IsDisabled, Guid? TeacherId, string? TeacherNameAr);
public sealed record CreateUserRequest(string Email, string Password, string Role, Guid? TeacherId);
public sealed record ResetPasswordRequest(string Password);
public sealed record SetDisabledRequest(bool Disabled);
public sealed record LinkTeacherRequest(Guid? TeacherId);

// ---------- Availability (admin review) ----------
public sealed record AvailabilityAdminDto(
    Guid Id, Guid TeacherId, string TeacherNameAr, DateTime StartUtc, DateTime EndUtc, string? Note, string Status, string? ReviewNote, DateTime CreatedAtUtc);
public sealed record ReviewAvailabilityRequest(bool Approve, string? Note);

// ---------- Promo codes ----------
public sealed record PromoCodeAdminDto(
    Guid? Id, string Code, string? Description, string Kind, decimal Value,
    Guid? GradeId, Guid? SubjectId, Guid? ServiceTypeId, Guid? LessonId, Guid? SessionId,
    DateTime? ValidFromUtc, DateTime? ValidToUtc, int? MaxTotalUses, int? MaxUsesPerPhone, int? MaxUsesPerSession, bool IsActive,
    int Uses = 0, decimal TotalDiscount = 0, string? ScopeLabel = null);
