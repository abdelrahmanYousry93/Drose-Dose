namespace Dose.Contracts;

// ---------- Catalog ----------
public sealed record GradeDto(Guid Id, string Name, Guid StageId, string StageName, bool RequiresGuardianContact);
public sealed record StageDto(Guid Id, string Name, bool RequiresGuardianContact, IReadOnlyList<GradeDto> Grades);
public sealed record LookupDto(Guid Id, string Name);
public sealed record ServiceTypeDto(Guid Id, string Code, string Name, string? Description);

// ---------- Sessions ----------
public sealed record TeacherPublicDto(
    Guid Id, string DisplayName, string? PhotoUrl, string? Bio, string? Qualifications, int? YearsOfExperience);

public sealed record GroupTermsDto(
    string TargetTierName,
    int TargetMin,
    int TargetMax,
    decimal WorstCasePrice,
    decimal MaxExtraAmount,
    DateTime? TierDecisionAtUtc);

public sealed record SessionSummaryDto(
    Guid Id,
    string ScopeTitle,
    string SubjectName,
    string GradeName,
    string StudyTrackName,
    string ServiceTypeName,
    string Mode,
    DateTime StartUtc,
    DateTime EndUtc,
    int DurationMinutes,
    string TeacherName,
    string? TeacherPhotoUrl,
    decimal PricePerStudent,
    string Currency,
    int Capacity,
    int ConfirmedCount,
    int RemainingSeats,
    DateTime BookingDeadlineUtc);

public sealed record SessionDetailsDto(
    SessionSummaryDto Summary,
    string? ScopeDetails,
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

public sealed record PaymentMethodDto(Guid Id, string Code, string Name, string ReceivingAccount, string? Instructions);

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
    string WhatsAppMessage,
    TopUpViewDto? TopUp,
    DateTime FullRefundUntilUtc,
    bool CanCancel);

public sealed record TopUpViewDto(
    decimal Amount, string Currency, DateTime? RespondByUtc, string? Reason, string? Choice,
    string? ProofStatus, string? ProofRejectionReason, int DecisionCount, int MinParticipants);

public sealed record TopUpChoiceRequest(string Choice);
public sealed record CancelBookingRequest(bool AcceptNoRefund);

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
    double AgeMinutes,
    bool IsTopUp);

public sealed record RejectPaymentRequest(string Reason);

public sealed record SettingDto(string Key, string Value, string? DescriptionAr);
public sealed record UpdateSettingsRequest(IReadOnlyList<SettingDto> Settings);

public sealed record PublicSettingsDto(string WhatsAppBusinessNumber, string BusinessTimeZone, int FullRefundCancellationHours);

// ---------- Session requests (public) ----------
public sealed record GroupOptionDto(Guid Id, string Name, int MinParticipants, int MaxParticipants, decimal PricePerStudent);
public sealed record PriceOptionsDto(string Currency, decimal? PrivatePrice, IReadOnlyList<GroupOptionDto> GroupOptions);
public sealed record CreateSessionRequest(
    string StudentName, string ContactPhone, string? GuardianName, string? GuardianPhone,
    Guid GradeId, Guid StudyTrackId, Guid SubjectId, Guid ServiceTypeId, string ScopeText,
    IReadOnlyList<string> PreferredDays, IReadOnlyList<string> PreferredTimes, string Mode, Guid? PreferredTierId, string? Notes);
public sealed record CreateSessionRequestResponse(string Reference, string WhatsAppMessage);

// ---------- Teacher area ----------
public sealed record TeacherMeDto(Guid TeacherId, string DisplayName, string Email);
public sealed record TeacherSessionDto(
    Guid Id, string Title, string? Details, string SubjectName, string GradeName, string StudyTrackName,
    DateTime StartUtc, DateTime EndUtc, string Mode, string Status, int ConfirmedCount, int Capacity, string? MeetingUrl,
    bool CanMarkAttendance, int AttendanceMarked);
public sealed record TeacherStudentDto(Guid BookingId, string FirstName, string? Attendance);
public sealed record TeacherSessionDetailsDto(TeacherSessionDto Session, IReadOnlyList<TeacherStudentDto> Students);
public sealed record MarkAttendanceRequest(IReadOnlyList<AttendanceItem> Items);
public sealed record AttendanceItem(Guid BookingId, bool Attended);
public sealed record AvailabilityDto(Guid Id, DateTime StartUtc, DateTime EndUtc, string? Note, string Status, string? ReviewNote);
public sealed record SubmitAvailabilityRequest(DateTime StartUtc, DateTime EndUtc, string? Note);
