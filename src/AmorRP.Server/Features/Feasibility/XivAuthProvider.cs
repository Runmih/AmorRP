using System.Net.Http.Headers;
using System.Text.Json;
using AmorRP.Contracts.Feasibility;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Feasibility;

public interface ICharacterIdentityProvider
{
    string AuthorizationUrl(string state, string challenge);
    Task<VerifiedCharacter?> VerifyAsync(string code, string verifier, IdentityProbeStart selected, CancellationToken cancellationToken);
}

// Provider URLs, scopes and wire formats stay in this adapter. No user/alt-list scope.
public sealed class XivAuthProvider(HttpClient client, IOptions<XivAuthOptions> options) : ICharacterIdentityProvider
{
    public const string Origin = "https://xivauth.net/";

    public string AuthorizationUrl(string state, string challenge) => QueryHelpers.AddQueryString(Origin + "oauth/authorize",
        new Dictionary<string, string?>
        {
            ["client_id"] = options.Value.ClientId, ["redirect_uri"] = options.Value.CallbackUrl,
            ["response_type"] = "code", ["scope"] = "character", ["state"] = state,
            ["code_challenge"] = challenge, ["code_challenge_method"] = "S256"
        });

    public async Task<VerifiedCharacter?> VerifyAsync(string code, string verifier, IdentityProbeStart selected, CancellationToken cancellationToken)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["code_verifier"] = verifier,
            ["client_id"] = options.Value.ClientId, ["client_secret"] = options.Value.ClientSecret,
            ["redirect_uri"] = options.Value.CallbackUrl
        });
        using var tokenResponse = await client.PostAsync(Origin + "oauth/token", form, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var token = tokenJson.RootElement;
        if (!string.Equals(token.GetProperty("token_type").GetString(), "Bearer", StringComparison.OrdinalIgnoreCase)
            || !token.TryGetProperty("scope", out var scopes) || scopes.GetString() != "character")
            throw new InvalidOperationException("Unexpected provider grant.");
        var accessToken = token.GetProperty("access_token").GetString();
        if (string.IsNullOrEmpty(accessToken)) throw new InvalidOperationException("Missing provider grant.");
        var url = QueryHelpers.AddQueryString(Origin + "api/v1/characters",
            new Dictionary<string, string?> { ["name"] = selected.DisplayName, ["home_world"] = selected.HomeWorld });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return ParseCharacter(json.RootElement, selected);
    }

    public static VerifiedCharacter? ParseCharacter(JsonElement root, IdentityProbeStart selected)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1) return null;
        var character = root[0];
        if (!character.TryGetProperty("verified_at", out var verified) || verified.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(verified.GetString(), out _)) return null;
        var name = character.GetProperty("name").GetString();
        var world = character.GetProperty("home_world").GetString();
        var key = character.GetProperty("persistent_key").GetString();
        var lodestone = character.GetProperty("lodestone_id").ToString();
        if (name != selected.DisplayName || world != selected.HomeWorld || string.IsNullOrWhiteSpace(key)
            || key.Length > 128 || !ulong.TryParse(lodestone, out var id) || id == 0) return null;
        return new(key, lodestone, name, world);
    }
}
