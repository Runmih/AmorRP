using AmorRP.Contracts.Groups;
namespace AmorRP.Server.Features.Groups;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this WebApplication app)
    {
        app.MapPut("/api/v2/seasons/{seasonId}/groups/{groupId}/trust", (HttpContext h, Guid seasonId, Guid groupId, GroupTrustEdit b, GroupService s, CancellationToken ct) => s.TrustAsync(h, seasonId, groupId, b, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups", (HttpContext h, Guid seasonId, GroupService s, CancellationToken ct) => s.ListAsync(h, seasonId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups", (HttpContext h, Guid seasonId, GroupWrite b, GroupService s, CancellationToken ct) => s.WriteAsync(h, seasonId, null, b, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}", (HttpContext h, Guid seasonId, Guid groupId, GroupService s, CancellationToken ct) => s.ReadAsync(h, seasonId, groupId, ct));
        app.MapPatch("/api/v2/seasons/{seasonId}/groups/{groupId}", (HttpContext h, Guid seasonId, Guid groupId, GroupWrite b, GroupService s, CancellationToken ct) => s.WriteAsync(h, seasonId, groupId, b, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}", (HttpContext h, Guid seasonId, Guid groupId, GroupService s, CancellationToken ct) => s.WriteAsync(h, seasonId, groupId, null, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/groups/{groupId}/features", (HttpContext h, Guid seasonId, Guid groupId, GroupFeaturesEdit b, GroupService s, CancellationToken ct) => s.FeaturesAsync(h, seasonId, groupId, b, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/groups/{groupId}/members/{characterId}/capabilities", (HttpContext h, Guid seasonId, Guid groupId, Guid characterId, GroupStaffEdit b, GroupService s, CancellationToken ct) => s.StaffAsync(h, seasonId, groupId, characterId, b, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/logo", (HttpContext h, Guid seasonId, Guid groupId, GroupService s, CancellationToken ct) => s.LogoAsync(h, seasonId, groupId, null, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/groups/{groupId}/logo", (HttpContext h, Guid seasonId, Guid groupId, GroupService s, CancellationToken ct) => s.LogoAsync(h, seasonId, groupId, true, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}/logo", (HttpContext h, Guid seasonId, Guid groupId, GroupService s, CancellationToken ct) => s.LogoAsync(h, seasonId, groupId, false, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/group-hub", (HttpContext h, Guid seasonId, GroupActivityService s, CancellationToken ct) => s.HubAsync(h, seasonId, ct));
        app.MapGet("/api/v2/me/favorite-group-events", (HttpContext h, GroupActivityService s, CancellationToken ct) => s.FavoritesAsync(h, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/private", (HttpContext h, Guid seasonId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.PrivateAsync(h, seasonId, groupId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/join", (HttpContext h, Guid seasonId, Guid groupId, GroupJoin b, GroupActivityService s, CancellationToken ct) => s.JoinAsync(h, seasonId, groupId, b, ct)).RequireRateLimiting("group-join");
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}/membership", (HttpContext h, Guid seasonId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.LeaveAsync(h, seasonId, groupId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/groups/{groupId}/favorite", (HttpContext h, Guid seasonId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.FavoriteAsync(h, seasonId, groupId, true, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}/favorite", (HttpContext h, Guid seasonId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.FavoriteAsync(h, seasonId, groupId, false, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/groups/{groupId}/notice", (HttpContext h, Guid seasonId, Guid groupId, NoticeWrite b, GroupActivityService s, CancellationToken ct) => s.NoticeAsync(h, seasonId, groupId, b, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}/notice", (HttpContext h, Guid seasonId, Guid groupId, GroupActivityService s, CancellationToken ct) => s.NoticeAsync(h, seasonId, groupId, null, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/events", (HttpContext h, Guid seasonId, Guid groupId, EventWrite b, GroupActivityService s, CancellationToken ct) => s.EventAsync(h, seasonId, groupId, b, null, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/groups/{groupId}/events/{eventId}", (HttpContext h, Guid seasonId, Guid groupId, Guid eventId, EventWrite b, GroupActivityService s, CancellationToken ct) => s.EventAsync(h, seasonId, groupId, b, eventId, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}/events/{eventId}", (HttpContext h, Guid seasonId, Guid groupId, Guid eventId, GroupActivityService s, CancellationToken ct) => s.EventAsync(h, seasonId, groupId, null, eventId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/announcements", (HttpContext h, Guid seasonId, Guid groupId, AnnouncementWrite b, GroupActivityService s, CancellationToken ct) => s.AnnouncementAsync(h, seasonId, groupId, b, ct));
    }
}
