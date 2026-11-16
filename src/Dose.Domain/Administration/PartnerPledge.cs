using Dose.Domain.Common;

namespace Dose.Domain.Administration;

/// <summary>
/// An organization's non-binding pledge to fund lesson "doses" for students (or to partner in another way), sent from
/// the public /partners page — e.g. at an event via its QR code. No money changes hands here: the owner follows up.
/// The organization's name appears on the public pledge wall only when ShowOnWall is true.
/// </summary>
public sealed class PartnerPledge : Entity
{
    private PartnerPledge() { }

    public PartnerPledge(string organization, string contactName, string contactPhoneE164, string? role, string interest,
        int doses, bool showOnWall, string? note, string? source, DateTime nowUtc)
    {
        Organization = organization;
        ContactName = contactName;
        ContactPhoneE164 = contactPhoneE164;
        Role = role;
        Interest = interest;
        Doses = doses;
        ShowOnWall = showOnWall;
        Note = note;
        Source = source;
        CreatedAtUtc = nowUtc;
        Reference = "P-" + Convert.ToHexString(Guid.NewGuid().ToByteArray(), 0, 3);
    }

    public string Reference { get; private set; } = "";
    public string Organization { get; private set; } = "";
    public string ContactName { get; private set; } = "";
    public string ContactPhoneE164 { get; private set; } = "";
    public string? Role { get; private set; }
    /// <summary>fund | students | employees | content | other</summary>
    public string Interest { get; private set; } = "";
    /// <summary>Lesson doses pledged (0 when the interest is not funding).</summary>
    public int Doses { get; private set; }
    public bool ShowOnWall { get; private set; }
    public string? Note { get; private set; }
    /// <summary>The ?src= code the visitor came with (e.g. ses26).</summary>
    public string? Source { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    /// <summary>Owner hid it from the wall (spam or a test).</summary>
    public bool Hidden { get; set; }

    public static readonly string[] Interests = ["fund", "students", "employees", "content", "other"];
    public const int MaxDoses = 100_000;
}
