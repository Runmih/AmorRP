using System.Security.Cryptography;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using AmorRP.Server.Options;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class PageCursor(SecretVault vault, IOptions<ServicePolicyOptions> policy)
{
    public (int Limit, Guid? After) Read(HttpContext http, string scope)
    {
        var rawLimit = http.Request.Query["limit"].ToString();
        var limit = string.IsNullOrEmpty(rawLimit) ? Math.Min(50, policy.Value.MaxPageSize) : int.TryParse(rawLimit, out var parsed) ? parsed : 0;
        ApiFault.Require(limit > 0 && limit <= policy.Value.MaxPageSize, 422, "invalid_request", "Invalid page size.");
        var cursor = http.Request.Query["cursor"].ToString();
        if (cursor.Length == 0) return (limit, null);
        try
        {
            ApiFault.Require(cursor.Length <= 1000, 422, "invalid_request", "Invalid page cursor.");
            var plain = vault.Unprotect(System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor)), "cursor:" + scope);
            return (limit, Guid.Parse(plain));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or KeyNotFoundException)
        { throw new ApiFault(422, "invalid_request", "Page cursor does not belong to this list."); }
    }
    public string Write(Guid last, string scope) => WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(vault.Protect(last.ToString(), "cursor:" + scope)));
}
