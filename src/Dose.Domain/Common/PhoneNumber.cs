namespace Dose.Domain.Common;

/// <summary>Normalizes Egyptian mobile numbers (and other international numbers) to E.164.</summary>
public static class PhoneNumber
{
    /// <summary>
    /// Accepts 01xxxxxxxxx, 1xxxxxxxxx, +201xxxxxxxxx, 00201xxxxxxxxx, and other +country numbers.
    /// Arabic-Indic digits are accepted. Returns null when invalid.
    /// </summary>
    public static string? TryNormalize(string? input, string defaultCountryCode = "20")
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var digits = new System.Text.StringBuilder();
        var hasPlus = false;
        foreach (var ch in input.Trim())
        {
            if (ch == '+' && digits.Length == 0) hasPlus = true;
            else if (char.IsDigit(ch)) digits.Append((char)('0' + (int)char.GetNumericValue(ch)));
            else if (ch is ' ' or '-' or '(' or ')' or '.') continue;
            else return null;
        }

        var d = digits.ToString();
        if (!hasPlus && d.StartsWith("00")) { d = d[2..]; hasPlus = true; }

        if (!hasPlus)
        {
            if (defaultCountryCode == "20")
            {
                if (d.Length == 11 && d.StartsWith("01")) d = "20" + d[1..];
                else if (d.Length == 10 && d.StartsWith('1')) d = "20" + d;
                else if (!(d.Length == 12 && d.StartsWith("201"))) return null;
            }
            else d = defaultCountryCode + d.TrimStart('0');
        }

        if (d.StartsWith("20"))
        {
            // Egyptian mobiles: +20 1[0125] xxxxxxxx
            return d.Length == 12 && d[2] == '1' && "0125".Contains(d[3]) ? "+" + d : null;
        }

        return d.Length is >= 8 and <= 15 ? "+" + d : null;
    }
}
