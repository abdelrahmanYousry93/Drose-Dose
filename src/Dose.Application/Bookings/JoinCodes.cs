using System.Security.Cryptography;
using Dose.Application.Abstractions;
using Dose.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Bookings;

/// <summary>
/// Per-session join codes (e.g. "K7") for confirmed bookings, and the log of "join session" clicks. The student writes
/// the code next to their name in the meeting; the host admits only codes on the attendance list, and a code seen twice
/// (or one booking opened from several devices) shows the link was shared.
/// </summary>
public static class JoinCodes
{
    const string Letters = "ACDEFHJKMNPRTUVWXY"; // no look-alikes (B/8, I/1, O/0, S/5, Z/2, G/6, L)
    const string Digits = "3479";
    public const string AuditEntity = "Join";
    static readonly BookingStatus[] Attending = [BookingStatus.Confirmed, BookingStatus.Completed];

    /// <summary>Gives every confirmed booking of the session a code (idempotent); returns booking id → code.</summary>
    public static async Task<Dictionary<Guid, string>> EnsureAsync(IDoseDbContext db, Guid sessionId, CancellationToken ct)
    {
        var rows = await db.Bookings.Where(b => b.SessionId == sessionId && Attending.Contains(b.Status)).ToListAsync(ct);
        var used = rows.Where(b => b.JoinCode != null).Select(b => b.JoinCode!).ToHashSet();
        // Codes of cancelled bookings stay reserved so an old code can never point at someone else.
        used.UnionWith(await db.Bookings.Where(b => b.SessionId == sessionId && b.JoinCode != null).Select(b => b.JoinCode!).ToListAsync(ct));
        var changed = false;
        foreach (var b in rows.Where(b => b.JoinCode is null).OrderBy(b => b.ConfirmedAtUtc ?? b.CreatedAtUtc))
        {
            string code;
            do code = $"{Letters[RandomNumberGenerator.GetInt32(Letters.Length)]}{Digits[RandomNumberGenerator.GetInt32(Digits.Length)]}";
            while (!used.Add(code) && used.Count < Letters.Length * Digits.Length);
            b.JoinCode = code;
            changed = true;
        }
        if (changed) await db.SaveChangesAsync(ct);
        return rows.ToDictionary(b => b.Id, b => b.JoinCode!);
    }

    /// <summary>A short, non-reversible device fingerprint (browser + network) so the log never stores the raw IP.</summary>
    public static string Device(string? userAgent, string? ip) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{userAgent}|{ip}")))[..12];
}
