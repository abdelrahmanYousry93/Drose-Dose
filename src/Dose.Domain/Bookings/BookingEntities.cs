using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;

namespace Dose.Domain.Bookings;

public enum BookingStatus
{
    PendingHold = 0,
    PendingPayment = 1,
    PaymentUnderReview = 2,
    Confirmed = 3,
    TopUpPending = 4,
    Cancelled = 5,
    Expired = 6,
    RefundPending = 7,
    Refunded = 8,
    Transferred = 9,
    Completed = 10,
    NoShow = 11,
}

public static class BookingStatuses
{
    /// <summary>Statuses that occupy a seat (subject to expiry for holds and unpaid bookings).</summary>
    public static readonly BookingStatus[] SeatOccupying =
    [
        BookingStatus.PendingHold,
        BookingStatus.PendingPayment,
        BookingStatus.PaymentUnderReview,
        BookingStatus.Confirmed,
        BookingStatus.TopUpPending,
    ];
}

public static class BookingErrors
{
    public const string SessionNotFound = "booking.session_not_found";
    public const string SessionClosed = "booking.session_closed";
    public const string SessionFull = "booking.session_full";
    public const string Duplicate = "booking.duplicate";
    public const string NotFound = "booking.not_found";
    public const string Expired = "booking.expired";
    public const string InvalidState = "booking.invalid_state";
    public const string GuardianRequired = "booking.guardian_required";
    public const string TermsRequired = "booking.terms_required";
    public const string PriceChanged = "booking.price_changed";
    public const string InvalidPhone = "booking.invalid_phone";
    public const string InvalidFile = "booking.invalid_file";
    public const string JoinNotAvailable = "booking.join_not_available";
    public const string NoTopUp = "booking.no_topup";
    public const string CancelNotAllowed = "booking.cancel_not_allowed";
}

/// <summary>Student contact captured at booking. No login.</summary>
public sealed class Student : Entity
{
    public required string Name { get; set; }
    public required string ContactPhoneE164 { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhoneE164 { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Booking : Entity
{
    /// <summary>Short public reference, e.g. DS-7K3Q9P. Not a secret.</summary>
    public required string Reference { get; init; }
    public Guid SessionId { get; init; }
    public Session? Session { get; set; }
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }

    /// <summary>Copy of the contact phone, used by the duplicate-booking unique index.</summary>
    public string? ContactPhoneE164 { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.PendingHold;
    public DateTime CreatedAtUtc { get; init; }
    /// <summary>Language the student booked in ("ar"/"en"); used for messages.</summary>
    public string Language { get; set; } = "ar";
    public DateTime? HoldExpiresAtUtc { get; set; }
    public DateTime? PaymentDueAtUtc { get; set; }

    /// <summary>Amount this booking pays (after any promo code). Rechecked on details submission.</summary>
    public decimal QuotedPrice { get; set; }
    /// <summary>Price before discount (per-student price × seats).</summary>
    public decimal ListPrice { get; set; }

    /// <summary>Seats this booking pays for: 1 normally, N for a private-group organizer, 0 for an invited friend.</summary>
    public int SeatCount { get; set; } = 1;
    /// <summary>For an invited friend: the organizer's booking that paid for the seat.</summary>
    public Guid? SponsorBookingId { get; set; }
    /// <summary>For a private-group organizer (after payment): the code friends use to join. Not a secret link to this booking.</summary>
    public string? InviteCode { get; set; }

    public Guid? PromoCodeId { get; set; }
    /// <summary>Percentage discount from the promo (also applied to any later group top-up).</summary>
    public decimal? DiscountPercent { get; set; }
    /// <summary>Discount taken off the list price.</summary>
    public decimal DiscountAmount { get; set; }
    public decimal? QuotedWorstCasePrice { get; set; }
    public required string Currency { get; set; }

    public DateTime? TermsAcceptedAtUtc { get; set; }
    public DateTime? TopUpTermsAcceptedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    /// <summary>Set when an admin finished a follow-up (refund paid, transfer arranged).</summary>
    public DateTime? FollowUpResolvedAtUtc { get; set; }

    public Guid? PriceSnapshotId { get; set; }
    public PriceSnapshot? PriceSnapshot { get; set; }

    /// <summary>SHA-256 of the secret access token in the student's private link.</summary>
    public required byte[] AccessTokenHash { get; set; }

    /// <summary>
    /// The secret token encrypted with the server's data-protection key, so the team can resend the student's
    /// link in messages without invalidating it. Never returned by any API except when composing a message.
    /// </summary>
    public string? AccessTokenProtected { get; set; }

    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// What this booking owes for a given list price, keeping its promo: a percentage applies again (so a group
    /// top-up keeps the same discount); a fixed amount is taken off once.
    /// </summary>
    public decimal NetPrice(decimal listPrice) => DiscountPercent is { } p
        ? Math.Round(listPrice * (100 - p) / 100, 2, MidpointRounding.AwayFromZero)
        : Math.Max(0, listPrice - DiscountAmount);

    public bool IsExpired(DateTime nowUtc) => Status switch
    {
        BookingStatus.PendingHold => HoldExpiresAtUtc is { } h && h <= nowUtc,
        BookingStatus.PendingPayment => PaymentDueAtUtc is { } p && p <= nowUtc,
        _ => false,
    };
}

public sealed class PaymentMethod : Entity
{
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    /// <summary>InstaPay address or wallet number shown to the student. Admin-managed.</summary>
    public required string ReceivingAccount { get; set; }
    public string? InstructionsAr { get; set; }
    public string? InstructionsEn { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum PaymentProofStatus
{
    Submitted = 0,
    Approved = 1,
    Rejected = 2,
}

public enum PaymentChannel
{
    Platform = 0,
    WhatsApp = 1,
}

public sealed class PaymentProof : Entity
{
    public Guid BookingId { get; init; }
    public Booking? Booking { get; set; }
    /// <summary>Set when this proof pays a top-up (not the original booking price).</summary>
    public Guid? AdjustmentId { get; init; }
    public FinancialAdjustment? Adjustment { get; set; }
    public Guid PaymentMethodId { get; init; }
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public string? SenderReference { get; init; }
    public required string FileKey { get; init; }
    public required string FileContentType { get; init; }
    /// <summary>Set when the screenshot file was deleted under the retention policy (the payment record stays).</summary>
    public DateTime? FileDeletedAtUtc { get; set; }
    public PaymentChannel Channel { get; init; }
    public PaymentProofStatus Status { get; set; } = PaymentProofStatus.Submitted;
    public DateTime SubmittedAtUtc { get; init; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
}
