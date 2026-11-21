namespace AmorRP.Plugin.Services.Api;
public sealed record CommandRequest(string Method, string Path, string? Json, string? ETag, string? UploadBase64 = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal CommandOperation Operation => CommandRoutes.Resolve(this);
    internal bool AppliesToSeason(Guid? activeId)
    {
        var path = Path.Split('/');
        return path.Length < 4 || path[2] != "seasons" || !Guid.TryParse(path[3], out var id) || id == activeId;
    }
    internal Guid? TargetGroupId()
    {
        var path = Path.Split('/');
        return path.Length >= 6 && path[4] == "groups" && Guid.TryParse(path[5], out var id) ? id : null;
    }
    internal bool AppliesToGroup(Guid? activeId) => TargetGroupId() is not { } id || id == activeId;
}
