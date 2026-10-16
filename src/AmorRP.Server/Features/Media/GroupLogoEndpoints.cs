namespace AmorRP.Server.Features.Media;

public static class GroupLogoEndpoints
{
    public static void MapGroupLogoEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/groups/{groupId}/logo", (HttpContext h, Guid groupId, GroupLogoService s, CancellationToken ct) => s.ReadAsync(h, groupId, ct));
        app.MapPut("/api/v1/groups/{groupId}/logo", (HttpContext h, Guid groupId, GroupLogoService s, CancellationToken ct) => s.ChangeAsync(h, groupId, true, ct));
        app.MapDelete("/api/v1/groups/{groupId}/logo", (HttpContext h, Guid groupId, GroupLogoService s, CancellationToken ct) => s.ChangeAsync(h, groupId, false, ct));
    }
}
