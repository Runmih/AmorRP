using System.Security.Cryptography;
using System.Text;
using AmorRP.Server.Infrastructure.Security;

namespace AmorRP.Server.Features.Authentication;

internal static class LoginPairingCode
{
    // 64 random bits, with a persistent per-attempt guess limit. No part of this
    // secret is returned through start, polling, OAuth state or a replayable URL.
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    public static bool Matches(string? supplied, string? expectedHash)
    {
        if (supplied == null || supplied.Length > 64 || expectedHash == null) return false;
        var normalized = supplied.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length != 16 || normalized.Any(c => !char.IsAsciiHexDigit(c))) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(SecretVault.Hash(normalized)), Encoding.ASCII.GetBytes(expectedHash));
    }
}
