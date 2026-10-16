using AmorRP.Contracts.Announcements;
namespace AmorRP.Server.Features.Announcements;

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/announcements/unread", (HttpContext h, AnnouncementService s, CancellationToken ct) => s.UnreadAsync(h, ct));
        app.MapGet("/api/v1/groups/{groupId}/announcements", (HttpContext h, Guid groupId, AnnouncementService s, CancellationToken ct) => s.ListAsync(h, groupId, ct));
        app.MapPost("/api/v1/groups/{groupId}/announcements", (HttpContext h, Guid groupId, AnnouncementCreate b, AnnouncementService s, CancellationToken ct) => s.CreateAsync(h, groupId, b, ct));
        app.MapGet("/api/v1/groups/{groupId}/announcements/{announcementId}", (HttpContext h, Guid groupId, Guid announcementId, AnnouncementService s, CancellationToken ct) => s.ReadAsync(h, groupId, announcementId, ct));
        app.MapPut("/api/v1/groups/{groupId}/announcements/{announcementId}/read", (HttpContext h, Guid groupId, Guid announcementId, AnnouncementService s, CancellationToken ct) => s.MarkReadAsync(h, groupId, announcementId, ct));
    }
}
