using System.Security.Cryptography;
using System.Text;

namespace Dose.Application.Bookings;

/// <summary>
/// Secret booking links: 256-bit random token in the URL, only its SHA-256 hash is stored.
/// Possession of the link is what authorizes the student to view the booking and join the session.
/// </summary>
public static class AccessTokens
{
    public static (string Token, byte[] Hash) Create()
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public static bool LooksValid(string? token) =>
        token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>Short human-friendly reference (not secret). Ambiguous characters removed.</summary>
    public static string NewReference()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return "DS-" + new string(chars);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class ImageSniffer
{
    /// <summary>Detects JPEG/PNG/WebP by content (magic bytes), never by file extension.</summary>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ("image/png", ".png");
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
            return ("image/webp", ".webp");
        return null;
    }
}
