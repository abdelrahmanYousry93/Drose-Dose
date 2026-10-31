using Dose.Application.Bookings;
using Dose.Domain.Common;

namespace Dose.UnitTests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("01001234567", "+201001234567")]
    [InlineData("0100 123 4567", "+201001234567")]
    [InlineData("1001234567", "+201001234567")]
    [InlineData("+20 100 123 4567", "+201001234567")]
    [InlineData("00201001234567", "+201001234567")]
    [InlineData("201001234567", "+201001234567")]
    [InlineData("٠١٠٠١٢٣٤٥٦٧", "+201001234567")]   // Arabic-Indic digits
    [InlineData("01112223334", "+201112223334")]
    [InlineData("01212223334", "+201212223334")]
    [InlineData("01512223334", "+201512223334")]
    [InlineData("+966501234567", "+966501234567")]
    public void Normalizes_valid_numbers(string input, string expected) =>
        Assert.Equal(expected, PhoneNumber.TryNormalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0100123456")]      // too short
    [InlineData("01301234567")]     // not an Egyptian mobile prefix
    [InlineData("0221234567")]      // landline
    [InlineData("01001234567x")]
    [InlineData("abc")]
    public void Rejects_invalid_numbers(string? input) => Assert.Null(PhoneNumber.TryNormalize(input));
}

public class AccessTokenTests
{
    [Fact]
    public void Token_is_url_safe_and_verifiable_by_hash()
    {
        var (token, hash) = AccessTokens.Create();
        Assert.True(AccessTokens.LooksValid(token));
        Assert.Equal(hash, AccessTokens.Hash(token));
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Tokens_are_unique()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => AccessTokens.Create().Token).ToHashSet();
        Assert.Equal(1000, tokens.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("../../../../etc/passwd/aaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Malformed_tokens_rejected(string token) => Assert.False(AccessTokens.LooksValid(token));

    [Fact]
    public void Reference_has_expected_shape()
    {
        var r = AccessTokens.NewReference();
        Assert.Matches("^DS-[A-HJ-NP-Z2-9]{8}$", r);
    }
}

public class ImageSnifferTests
{
    [Fact]
    public void Detects_png_jpeg_webp_by_content()
    {
        Assert.Equal("image/png", ImageSniffer.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0])?.ContentType);
        Assert.Equal("image/jpeg", ImageSniffer.Detect([0xFF, 0xD8, 0xFF, 0xE0])?.ContentType);
        Assert.Equal("image/webp", ImageSniffer.Detect("RIFF\0\0\0\0WEBPVP8 "u8.ToArray())?.ContentType);
    }

    [Fact]
    public void Rejects_other_content()
    {
        Assert.Null(ImageSniffer.Detect("MZ\x90\0executable"u8.ToArray()));
        Assert.Null(ImageSniffer.Detect("<svg xmlns=\"\">"u8.ToArray()));
        Assert.Null(ImageSniffer.Detect([]));
    }
}
