using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;

namespace AmorRP.Plugin.Services.Identity;

[Serializable]
public sealed class SavedSession
{
    public string Origin { get; set; } = "";
    public Guid CharacterId { get; set; }
    public string DisplayName { get; set; } = "";
    public string HomeWorldName { get; set; } = "";
    public string ProtectedTokens { get; set; } = "";
    public Guid? PendingRefreshKey { get; set; }
    public Guid? SelectedGroupId { get; set; }
    public static string Protect(SessionTokens tokens) => Convert.ToBase64String(ProtectedData.Protect(
        JsonSerializer.SerializeToUtf8Bytes(tokens), Encoding.UTF8.GetBytes("AmorRP.session.v1"), DataProtectionScope.CurrentUser));
    public SessionTokens Read() => JsonSerializer.Deserialize<SessionTokens>(ProtectedData.Unprotect(
        Convert.FromBase64String(ProtectedTokens), Encoding.UTF8.GetBytes("AmorRP.session.v1"), DataProtectionScope.CurrentUser))
        ?? throw new CryptographicException("Saved login is invalid.");
}
[Serializable]
public sealed class PendingCommand
{
    public string Origin { get; set; } = "";
    public Guid CharacterId { get; set; }
    public Guid Key { get; set; }
    public string ProtectedRequest { get; set; } = "";
    public static string Protect(CommandRequest request) => Convert.ToBase64String(ProtectedData.Protect(
        JsonSerializer.SerializeToUtf8Bytes(request), Encoding.UTF8.GetBytes("AmorRP.command.v1"), DataProtectionScope.CurrentUser));
    public CommandRequest Read() => JsonSerializer.Deserialize<CommandRequest>(ProtectedData.Unprotect(
        Convert.FromBase64String(ProtectedRequest), Encoding.UTF8.GetBytes("AmorRP.command.v1"), DataProtectionScope.CurrentUser))!;
}
