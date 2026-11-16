using Dose.Domain.Common;

namespace Dose.Domain.Administration;

/// <summary>
/// A private "my bookings" link for one phone number (no student accounts, no passwords). The student asks for it from
/// their own WhatsApp — the message itself proves they own the number — and the team sends it back to that chat.
/// Opening it on any device restores every booking and request made with that number. Only the SHA-256 hash of the
/// token is stored; the link expires.
/// </summary>
public sealed class StudentAccessLink : Entity
{
    private StudentAccessLink() { }

    public StudentAccessLink(byte[] tokenHash, string phoneE164, string createdBy, DateTime nowUtc, TimeSpan validFor)
    {
        TokenHash = tokenHash;
        PhoneE164 = phoneE164;
        CreatedBy = createdBy;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc + validFor;
    }

    public byte[] TokenHash { get; private set; } = [];
    public string PhoneE164 { get; private set; } = "";
    public string CreatedBy { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public int Uses { get; set; }

    public static readonly TimeSpan Validity = TimeSpan.FromDays(90);
}
