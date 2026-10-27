using AmorRP.Contracts.Common;
namespace AmorRP.Plugin.Services.Api;
public sealed record CommandRequest(string Method, string Path, string? Json, string? ETag, string? UploadBase64 = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal CommandOperation Operation => CommandRoutes.Resolve(this);
    public CommandRequest CurrentShape() => Path.StartsWith("api/v1/", StringComparison.Ordinal)
        ? this with { Path = LegacyCommunityJson.CurrentPath(Path), Json = Json == null ? null : LegacyCommunityJson.ToCurrent(Json), ETag = ETag == null ? null : LegacyCommunityJson.ETag(ETag, legacy: false) } : this;
}
