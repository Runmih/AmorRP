using AmorRP.Contracts.Common;
namespace AmorRP.Plugin.Services.Api;
public sealed record CommandRequest(string Method, string Path, string? Json, string? ETag, string? UploadBase64 = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal CommandOperation Operation => CommandRoutes.Resolve(this);
    internal bool AppliesToCommunity(Guid? activeId)
    {
        var path = CurrentShape().Path.Split('/');
        return path.Length < 4 || path[2] != "communities" || !Guid.TryParse(path[3], out var id) || id == activeId;
    }
    internal bool AppliesToGroup(Guid? activeId)
    {
        var path = CurrentShape().Path.Split('/');
        return path.Length < 6 || path[4] != "groups" || !Guid.TryParse(path[5], out var id) || id == activeId;
    }
    public CommandRequest CurrentShape() => Path.StartsWith("api/v1/", StringComparison.Ordinal)
        ? this with { Path = LegacyCommunityJson.CurrentPath(Path), Json = Json == null ? null : LegacyCommunityJson.ToCurrent(Json), ETag = ETag == null ? null : LegacyCommunityJson.ETag(ETag, legacy: false) } : this;
}
