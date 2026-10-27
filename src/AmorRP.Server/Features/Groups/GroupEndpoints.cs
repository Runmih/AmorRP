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
        app.MapGet("/api/v2/communities/{communityId}/group-hub", (HttpContext h, Guid communityId, GroupActivityService s, CancellationToken ct) => s.HubAsync(h, communityId, ct));
        app.MapGet("/api/v2/me/favorite-group-events", (HttpContext h, GroupActivityService s, CancellationToken ct) => s.FavoritesAsync(h, ct));
        app.MapGet("/api/v2/communities/{communityId}/groups/{groupId}/private", (HttpContext h, Guid communityId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.PrivateAsync(h, communityId, groupId, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups/{groupId}/join", (HttpContext h, Guid communityId, Guid groupId, GroupJoin b, GroupActivityService s, CancellationToken ct) => s.JoinAsync(h, communityId, groupId, b, ct)).RequireRateLimiting("group-join");
        app.MapDelete("/api/v2/communities/{communityId}/groups/{groupId}/membership", (HttpContext h, Guid communityId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.LeaveAsync(h, communityId, groupId, ct));
        app.MapPut("/api/v2/communities/{communityId}/groups/{groupId}/favorite", (HttpContext h, Guid communityId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.FavoriteAsync(h, communityId, groupId, true, ct));
        app.MapDelete("/api/v2/communities/{communityId}/groups/{groupId}/favorite", (HttpContext h, Guid communityId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.FavoriteAsync(h, communityId, groupId, false, ct));
        app.MapPut("/api/v2/communities/{communityId}/groups/{groupId}/notice", (HttpContext h, Guid communityId, Guid groupId, NoticeWrite b, GroupActivityService s, CancellationToken ct) => s.NoticeAsync(h, communityId, groupId, b, ct));
        app.MapDelete("/api/v2/communities/{communityId}/groups/{groupId}/notice", (HttpContext h, Guid communityId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.NoticeAsync(h, communityId, groupId, null, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups/{groupId}/events", (HttpContext h, Guid communityId, Guid groupId, EventWrite b, GroupActivityService s, CancellationToken ct) => s.EventAsync(h, communityId, groupId, b, null, ct));
        app.MapPut("/api/v2/communities/{communityId}/groups/{groupId}/events/{eventId}", (HttpContext h, Guid communityId, Guid groupId, Guid eventId, EventWrite b, GroupActivityService s, CancellationToken ct) => s.EventAsync(h, communityId, groupId, b, eventId, ct));
        app.MapDelete("/api/v2/communities/{communityId}/groups/{groupId}/events/{eventId}", (HttpContext h, Guid communityId, Guid groupId, Guid eventId, GroupActivityService s, CancellationToken ct) => s.EventAsync(h, communityId, groupId, null, eventId, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups/{groupId}/announcements", (HttpContext h, Guid communityId, Guid groupId, AnnouncementWrite b, GroupActivityService s, CancellationToken ct) => s.AnnouncementAsync(h, communityId, groupId, b, ct));
    }
}
