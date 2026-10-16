using System.Globalization;
namespace AmorRP.Contracts.Currency;

// Wire-format normalization shared by the plugin and server; no monetary authority.
public static class WholeUnits
{
    public static bool TryNormalize(string? input, out string normalized, out long value)
    {
        normalized = "0"; value = 0;
        if (input is not { Length: > 0 and <= 64 } || input.Any(c => c < '0' || c > '9')) return false;
        var digits = input.TrimStart('0'); if (digits.Length == 0) digits = "0";
        if (digits.Length > 19 || !long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
        normalized = value.ToString(CultureInfo.InvariantCulture); return true;
    }
}
