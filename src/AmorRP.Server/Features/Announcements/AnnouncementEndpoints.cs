using AmorRP.Contracts.Announcements;
namespace AmorRP.Server.Features.Announcements;

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/announcements/unread", (HttpContext h, AnnouncementService s, CancellationToken ct) => s.UnreadAsync(h, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/announcements", (HttpContext h, Guid seasonId, AnnouncementService s, CancellationToken ct) => s.ListAsync(h, seasonId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/announcements", (HttpContext h, Guid seasonId, AnnouncementCreate b, AnnouncementService s, CancellationToken ct) => s.CreateAsync(h, seasonId, b, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/announcements/{announcementId}", (HttpContext h, Guid seasonId, Guid announcementId, AnnouncementService s, CancellationToken ct) => s.ReadAsync(h, seasonId, announcementId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/announcements/{announcementId}/read", (HttpContext h, Guid seasonId, Guid announcementId, AnnouncementService s, CancellationToken ct) => s.MarkReadAsync(h, seasonId, announcementId, ct));
    }
}
