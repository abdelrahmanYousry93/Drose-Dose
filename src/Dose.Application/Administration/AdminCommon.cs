using System.Text.Json;
using Dose.Application.Abstractions;
using Dose.Domain.Administration;
using Dose.Domain.Common;

namespace Dose.Application.Administration;

public static class AdminErrors
{
    public const string Invalid = "admin.invalid";
    public const string NotFound = "admin.not_found";
    public const string Conflict = "admin.conflict";
}

internal static class AdminGuard
{
    public static string Name(string? value, string field = "الاسم", int max = 100)
    {
        var v = (value ?? "").Trim();
        if (v.Length < 2 || v.Length > max)
            throw new DomainException(AdminErrors.Invalid, $"{field} لازم يكون بين 2 و{max} حرف.");
        return v;
    }

    public static string? Optional(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > max) throw new DomainException(AdminErrors.Invalid, $"النص أطول من {max} حرف.");
        return v;
    }

    public static string? Url(string? value)
    {
        var v = Optional(value, 500);
        if (v is null) return null;
        if (!Uri.TryCreate(v, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new DomainException(AdminErrors.Invalid, "الرابط لازم يكون https كامل.");
        return v;
    }

    public static T Found<T>(T? entity, string what) where T : class =>
        entity ?? throw new DomainException(AdminErrors.NotFound, $"{what} مش موجود.");

    public static void Audit(this IDoseDbContext db, IClock clock, string actor, string entity, object id, string action, object? data = null)
    {
        var json = data is null ? null : JsonSerializer.Serialize(data);
        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor, Entity = entity, EntityId = id.ToString()!, Action = action,
            DataJson = json is null ? null : json[..Math.Min(json.Length, 4000)],
            AtUtc = clock.UtcNow,
        });
    }
}
