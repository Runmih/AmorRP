using System.Security.Cryptography;
using AmorRP.Server.Infrastructure.Security;

namespace AmorRP.Server.Features.Groups;

internal static class InvitationCode
{
    // 45 random bits, authenticated joins and a separate per-network join budget.
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public static readonly DateTimeOffset PermanentUntil = new(9999, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static string New() => new(Enumerable.Range(0, 9).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
    public static string Normalize(string value) => value.Trim().Length == 9 ? value.Trim().ToUpperInvariant() : value.Trim();
    public static bool IsValid(string? value) => value != null && (SecretVault.IsCredential(value.Trim())
        || Normalize(value) is { Length: 9 } code && code.All(Alphabet.Contains));
}
