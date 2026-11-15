namespace AmorRP.Contracts.Seasons;

public sealed record SeasonSummary(Guid Id, string Name, string State, int Version);
public sealed record SeasonContext(bool IsAdministrator, Guid? ActiveSeasonId, SeasonSummary[] Seasons, string[] MyCapabilities);
public sealed record SeasonTransition(string ConfirmName);
public sealed record SeasonCreationPermission(AmorRP.Contracts.Auth.Character Character, bool CanCreate, bool IsAdministrator, int Version, string Etag);
public sealed record SeasonCreationPermissionPage(SeasonCreationPermission[] Items, string? NextCursor);
public sealed record SeasonCreationPermissionEdit(bool CanCreate, string Reason);

public static class SeasonCapabilities
{
    public const string Create = "seasons.create";
}
