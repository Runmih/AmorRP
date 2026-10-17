namespace AmorRP.Server.Features.Media;

public static class CommunityLogoEndpoints
{
    public static void MapCommunityLogoEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/logo", (HttpContext h, Guid communityId, CommunityLogoService s, CancellationToken ct) => s.ReadAsync(h, communityId, ct));
        app.MapPut("/api/v2/communities/{communityId}/logo", (HttpContext h, Guid communityId, CommunityLogoService s, CancellationToken ct) => s.ChangeAsync(h, communityId, true, ct));
        app.MapDelete("/api/v2/communities/{communityId}/logo", (HttpContext h, Guid communityId, CommunityLogoService s, CancellationToken ct) => s.ChangeAsync(h, communityId, false, ct));
    }
}
