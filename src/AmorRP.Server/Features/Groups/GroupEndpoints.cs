using AmorRP.Contracts.Groups;
namespace AmorRP.Server.Features.Groups;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/groups", (HttpContext h, Guid communityId, GroupService s, CancellationToken ct) => s.ListAsync(h, communityId, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups", (HttpContext h, Guid communityId, GroupWrite b, GroupService s, CancellationToken ct) => s.WriteAsync(h, communityId, null, b, ct));
        app.MapGet("/api/v2/communities/{communityId}/groups/{groupId}", (HttpContext h, Guid communityId, Guid groupId, GroupService s, CancellationToken ct) => s.ReadAsync(h, communityId, groupId, ct));
        app.MapPatch("/api/v2/communities/{communityId}/groups/{groupId}", (HttpContext h, Guid communityId, Guid groupId, GroupWrite b, GroupService s, CancellationToken ct) => s.WriteAsync(h, communityId, groupId, b, ct));
        app.MapDelete("/api/v2/communities/{communityId}/groups/{groupId}", (HttpContext h, Guid communityId, Guid groupId, GroupService s, CancellationToken ct) => s.WriteAsync(h, communityId, groupId, null, ct));
        app.MapGet("/api/v2/communities/{communityId}/groups/{groupId}/logo", (HttpContext h, Guid communityId, Guid groupId, GroupService s, CancellationToken ct) => s.LogoAsync(h, communityId, groupId, null, ct));
        app.MapPut("/api/v2/communities/{communityId}/groups/{groupId}/logo", (HttpContext h, Guid communityId, Guid groupId, GroupService s, CancellationToken ct) => s.LogoAsync(h, communityId, groupId, true, ct));
        app.MapDelete("/api/v2/communities/{communityId}/groups/{groupId}/logo", (HttpContext h, Guid communityId, Guid groupId, GroupService s, CancellationToken ct) => s.LogoAsync(h, communityId, groupId, false, ct));
    }
}
