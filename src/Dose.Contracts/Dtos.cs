namespace Dose.Contracts;

// ---------- Catalog ----------
public sealed record GradeDto(Guid Id, string NameAr, Guid StageId, string StageNameAr, bool RequiresGuardianContact);
public sealed record StageDto(Guid Id, string NameAr, bool RequiresGuardianContact, IReadOnlyList<GradeDto> Grades);
public sealed record LookupDto(Guid Id, string NameAr);
public sealed record ServiceTypeDto(Guid Id, string Code, string NameAr, string? DescriptionAr);

// ---------- Sessions ----------
public sealed record TeacherPublicDto(
    Guid Id, string DisplayNameAr, string? PhotoUrl, string? BioAr, string? QualificationsAr, int? YearsOfExperience);

public sealed record GroupTermsDto(
    string TargetTierNameAr,
    int TargetMin,
    int TargetMax,
    decimal WorstCasePrice,
    decimal MaxExtraAmount,
    DateTime? TierDecisionAtUtc);

public sealed record SessionSummaryDto(
    Guid Id,
    string ScopeTitleAr,
    string SubjectNameAr,
    string GradeNameAr,
    string StudyTrackNameAr,
    string ServiceTypeNameAr,
    string Mode,
    DateTime StartUtc,
    DateTime EndUtc,
    int DurationMinutes,
    string TeacherNameAr,
    string? TeacherPhotoUrl,
    decimal PricePerStudent,
    string Currency,
    int Capacity,
    int ConfirmedCount,
    int RemainingSeats,
    DateTime BookingDeadlineUtc);

public sealed record SessionDetailsDto(
    SessionSummaryDto Summary,
    string? ScopeDetailsAr,
    int MinParticipants,
    TeacherPublicDto Teacher,
    GroupTermsDto? GroupTerms,
    bool RequiresGuardianContact,
    bool IsOpenForBooking);

// ---------- Booking ----------
public sealed record CreateHoldRequest(Guid SessionId);

public sealed record CreateHoldResponse(
    Guid BookingId,
    string Reference,
    string AccessToken,
    DateTime HoldExpiresAtUtc,
    decimal Price,
    decimal? WorstCasePrice,
    string Currency);

public sealed record SubmitDetailsRequest(
    string StudentName,
    string ContactPhone,
    string? GuardianName,
    string? GuardianPhone,
    bool AcceptTerms,
    bool AcceptTopUpTerms,
    decimal ExpectedPrice);

public sealed record PaymentMethodDto(Guid Id, string Code, string NameAr, string ReceivingAccount, string? InstructionsAr);

public sealed record BookingViewDto(
    string Reference,
    string Status,
    SessionSummaryDto Session,
    TeacherPublicDto Teacher,
    GroupTermsDto? GroupTerms,
    bool RequiresGuardianContact,
    decimal Price,
    string Currency,
    DateTime? HoldExpiresAtUtc,
    DateTime? PaymentDueAtUtc,
    string? StudentName,
    string? LatestProofStatus,
    string? LatestProofRejectionReason,
    bool CanJoinNow,
    DateTime? JoinOpensAtUtc,
    string WhatsAppMessage);

public sealed record JoinResponse(string MeetingUrl);

// ---------- Admin ----------
public sealed record PaymentProofAdminDto(
    Guid Id,
    string BookingReference,
    string BookingStatus,
    string StudentName,
    string ContactPhone,
    string? GuardianPhone,
    string SessionTitleAr,
    DateTime SessionStartUtc,
    string MethodNameAr,
    decimal Amount,
    string Currency,
    string? SenderReference,
    string Channel,
    string Status,
    DateTime SubmittedAtUtc,
    double AgeMinutes);

public sealed record RejectPaymentRequest(string Reason);

public sealed record SettingDto(string Key, string Value, string? DescriptionAr);
public sealed record UpdateSettingsRequest(IReadOnlyList<SettingDto> Settings);

public sealed record PublicSettingsDto(string WhatsAppBusinessNumber, string BusinessTimeZone);
