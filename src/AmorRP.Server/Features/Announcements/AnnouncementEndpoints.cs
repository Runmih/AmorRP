using AmorRP.Contracts.Announcements;
namespace AmorRP.Server.Features.Announcements;

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/announcements/unread", (HttpContext h, AnnouncementService s, CancellationToken ct) => s.UnreadAsync(h, ct));
        app.MapGet("/api/v2/communities/{communityId}/announcements", (HttpContext h, Guid communityId, AnnouncementService s, CancellationToken ct) => s.ListAsync(h, communityId, ct));
        app.MapPost("/api/v2/communities/{communityId}/announcements", (HttpContext h, Guid communityId, AnnouncementCreate b, AnnouncementService s, CancellationToken ct) => s.CreateAsync(h, communityId, b, ct));
        app.MapGet("/api/v2/communities/{communityId}/announcements/{announcementId}", (HttpContext h, Guid communityId, Guid announcementId, AnnouncementService s, CancellationToken ct) => s.ReadAsync(h, communityId, announcementId, ct));
        app.MapPut("/api/v2/communities/{communityId}/announcements/{announcementId}/read", (HttpContext h, Guid communityId, Guid announcementId, AnnouncementService s, CancellationToken ct) => s.MarkReadAsync(h, communityId, announcementId, ct));
    }
}
