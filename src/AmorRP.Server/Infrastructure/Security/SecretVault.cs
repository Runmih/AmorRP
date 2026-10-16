using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Infrastructure.Security;

public sealed class SecretVaultOptions
{
    public string ActiveKeyId { get; set; } = "";
    public Dictionary<string, string> Keys { get; set; } = [];
    public bool IsValid()
    {
        try { return Keys.ContainsKey(ActiveKeyId) && Keys.Count <= 8 && Keys.All(k => k.Key.Length is > 0 and <= 40
            && k.Key.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && Convert.FromBase64String(k.Value).Length == 32); }
        catch (FormatException) { return false; }
    }
}
// Versioned envelope encryption with platform AES-GCM; keys are deployment secrets,
// separate from the database. Retain old keys while records reference their IDs.
public sealed class SecretVault(IOptions<SecretVaultOptions> options)
{
    public static string NewSecret() => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool IsCredential(string? value) => value is { Length: 43 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public string[] LoginOriginHashes(string network)
        => options.Value.Keys.OrderByDescending(pair => pair.Key == options.Value.ActiveKeyId).Select(pair =>
        {
            // Purpose-derived HMAC key: do not store raw addresses or reversible
            // unkeyed IP hashes. Retained keys keep admission fair during rotation.
            var key = HMACSHA256.HashData(Convert.FromBase64String(pair.Value), Encoding.UTF8.GetBytes("amorrp:v1:login-origin-key"));
            return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(network)));
        }).Distinct(StringComparer.Ordinal).ToArray();
    public string Protect(string value, string purpose)
    {
        var keyId = options.Value.ActiveKeyId;
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(value);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(Convert.FromBase64String(options.Value.Keys[keyId]), 16);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes("amorrp:v1:" + purpose));
        return string.Join('.', "v1", keyId, Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher));
    }
    public string Unprotect(string value, string purpose)
    {
        var parts = value.Split('.');
        if (parts.Length != 5 || parts[0] != "v1") throw new CryptographicException("Invalid secret envelope.");
        var cipher = Convert.FromBase64String(parts[4]);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(Convert.FromBase64String(options.Value.Keys[parts[1]]), 16);
        aes.Decrypt(Convert.FromBase64String(parts[2]), cipher, Convert.FromBase64String(parts[3]), plain, Encoding.UTF8.GetBytes("amorrp:v1:" + purpose));
        return Encoding.UTF8.GetString(plain);
    }
}
