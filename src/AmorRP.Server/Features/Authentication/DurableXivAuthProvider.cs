using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Feasibility;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Authentication;

public sealed record ProviderIdentity(string LodestoneId, string OwnershipKey, string DisplayName, string HomeWorldName);
public sealed record ProviderGrant(ProviderIdentity Identity, string RefreshToken);
public interface IDurableIdentityProvider
{
    string AuthorizationUrl(string state, string challenge);
    Task<ProviderGrant?> ExchangeAsync(string code, string verifier, LoginStart selected, string? lodestoneId, CancellationToken ct);
    Task<ProviderGrant?> RenewAsync(string refresh, string lodestoneId, CancellationToken ct);
}
public sealed class DurableXivAuthProvider(HttpClient client, IOptions<XivAuthOptions> options) : IDurableIdentityProvider
{
    public string AuthorizationUrl(string state, string challenge) => QueryHelpers.AddQueryString(XivAuthProvider.Origin + "oauth/authorize",
        new Dictionary<string, string?> {
            ["client_id"] = options.Value.ClientId, ["redirect_uri"] = options.Value.DurableCallbackUrl,
            ["response_type"] = "code", ["scope"] = "character refresh", ["state"] = state,
            ["code_challenge"] = challenge, ["code_challenge_method"] = "S256"
        });
    public Task<ProviderGrant?> ExchangeAsync(string code, string verifier, LoginStart selected, string? lodestoneId, CancellationToken ct) =>
        GrantAsync(new() { ["grant_type"] = "authorization_code", ["code"] = code,
            ["code_verifier"] = verifier, ["redirect_uri"] = options.Value.DurableCallbackUrl }, selected, lodestoneId, ct);
    public Task<ProviderGrant?> RenewAsync(string refresh, string lodestoneId, CancellationToken ct) =>
        GrantAsync(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh }, null, lodestoneId, ct);
    private async Task<ProviderGrant?> GrantAsync(Dictionary<string, string> fields, LoginStart? selected, string? id, CancellationToken ct)
    {
        fields["client_id"] = options.Value.ClientId;
        fields["client_secret"] = options.Value.ClientSecret;
        using var form = new FormUrlEncodedContent(fields);
        using var tokens = await client.PostAsync(XivAuthProvider.Origin + "oauth/token", form, ct);
        if (tokens.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        tokens.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokens.Content.ReadAsStringAsync(ct));
        var token = tokenJson.RootElement;
        var scopes = token.GetProperty("scope").GetString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order().ToArray();
        if (!string.Equals(token.GetProperty("token_type").GetString(), "Bearer", StringComparison.OrdinalIgnoreCase)
            || scopes == null || !scopes.SequenceEqual(new[] { "character", "refresh" })
            || !token.TryGetProperty("refresh_token", out var refresh) || string.IsNullOrWhiteSpace(refresh.GetString())) return null;
        var url = id == null
            ? QueryHelpers.AddQueryString(XivAuthProvider.Origin + "api/v1/characters", new Dictionary<string, string?> {
                ["name"] = selected!.DisplayName, ["home_world"] = selected.HomeWorldName })
            : XivAuthProvider.Origin + "api/v1/characters/" + Uri.EscapeDataString(id);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        if (id == null)
        {
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1) return null;
            root = root[0];
        }
        var identity = Parse(root);
        if (identity == null || (id != null && identity.LodestoneId != id)
            || (selected != null && (identity.DisplayName != selected.DisplayName || identity.HomeWorldName != selected.HomeWorldName))) return null;
        return new(identity, refresh.GetString()!);
    }
    private static ProviderIdentity? Parse(JsonElement root)
    {
        if (!root.TryGetProperty("verified_at", out var verified) || !DateTimeOffset.TryParse(verified.GetString(), out _)) return null;
        var id = root.GetProperty("lodestone_id").ToString();
        var key = root.GetProperty("persistent_key").GetString();
        var name = root.GetProperty("name").GetString();
        var world = root.GetProperty("home_world").GetString();
        if (!ulong.TryParse(id, out var number) || number == 0 || string.IsNullOrWhiteSpace(key) || key.Length > 128
            || string.IsNullOrWhiteSpace(name) || name.Length > 80 || string.IsNullOrWhiteSpace(world) || world.Length > 80) return null;
        return new(id, key, name, world);
    }
}
