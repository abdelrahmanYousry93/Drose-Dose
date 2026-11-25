using System.Security.Claims;
using Dose.Infrastructure.Persistence;

namespace Dose.Infrastructure.Identity;

/// <summary>
/// Staff accounts (e.g. marketing) use the admin panel with page-level permissions the owner sets.
/// Permissions are stored as user claims of <see cref="ClaimType"/> and are read from the database on each
/// admin call, so a change takes effect immediately. Account management ("users") is never open to staff.
/// </summary>
public static class StaffAccess
{
    public const string ClaimType = "dose:area";

    /// <summary>Admin-panel pages the owner can open to a staff account (route names in /admin/...).</summary>
    public static readonly string[] Areas =
    [
        "dashboard", "funnel", "students", "partners", "payments", "follow-ups", "messages", "requests",
        "promos", "sessions", "pricing", "teachers", "catalog", "payment-methods", "availability", "settings",
    ];

    /// <summary>Lists any staff account may read, because other pages need them (names in dropdowns).</summary>
    static readonly HashSet<string> SharedReads = ["catalog", "teachers", "payment-methods", "grades", "stages", "subjects",
        "study-tracks", "service-types", "chapters", "lessons", "pricing-bands", "message-templates"];

    /// <summary>The page area an /api/admin/... call belongs to, by its first path segment; null = owner only.</summary>
    public static string? AreaOf(string firstSegment) => firstSegment switch
    {
        "dashboard" => "dashboard",
        "funnel" => "funnel",
        "students" => "students",
        "partners" => "partners",
        "payment-proofs" or "top-up-proofs" or "adjustments" or "refunds" => "payments",
        "follow-ups" => "follow-ups",
        "messages" or "message-templates" => "messages",
        "session-requests" => "requests",
        "promo-codes" => "promos",
        "sessions" or "bookings" => "sessions",
        "pricing" or "pricing-bands" => "pricing",
        "teachers" => "teachers",
        "catalog" or "grades" or "stages" or "subjects" or "study-tracks" or "service-types" or "chapters" or "lessons" => "catalog",
        "payment-methods" => "payment-methods",
        "availability" or "proposals" => "availability",
        "settings" => "settings",
        _ => null,
    };

    /// <summary>Whether a staff account with <paramref name="areas"/> may make this admin API call.</summary>
    public static bool Allows(IReadOnlyCollection<string> areas, string method, string firstSegment)
    {
        if (HttpMethods.IsGet(method) && SharedReads.Contains(firstSegment)) return true;
        var area = AreaOf(firstSegment);
        return area is not null && areas.Contains(area);
    }

    static class HttpMethods
    {
        public static bool IsGet(string m) => string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStaffOnly(ClaimsPrincipal u) =>
        u.IsInRole(DevelopmentSeeder.StaffRole) && !u.IsInRole(DevelopmentSeeder.AdminRole);
}
