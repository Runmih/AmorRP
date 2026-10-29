using AmorRP.Contracts.Currency;
namespace AmorRP.Server.Features.Currency;

public static class TreasuryEndpoints
{
    public static void MapTreasuryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/groups/{groupId}/treasury", (HttpContext h, Guid communityId, Guid groupId, TreasuryService s, CancellationToken ct) => s.BalanceAsync(h, communityId, groupId, ct));
        app.MapGet("/api/v2/communities/{communityId}/groups/{groupId}/treasury/records", (HttpContext h, Guid communityId, Guid groupId, TreasuryService s, CancellationToken ct) => s.RecordsAsync(h, communityId, groupId, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups/{groupId}/treasury/deposits", (HttpContext h, Guid communityId, Guid groupId, TreasuryTransfer b, TreasuryService s, CancellationToken ct) => s.TransferAsync(h, communityId, groupId, b, true, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups/{groupId}/treasury/withdrawals", (HttpContext h, Guid communityId, Guid groupId, TreasuryTransfer b, TreasuryService s, CancellationToken ct) => s.TransferAsync(h, communityId, groupId, b, false, ct));
        app.MapGet("/api/v2/me/treasury-returns", (HttpContext h, TreasuryService s, CancellationToken ct) => s.ReturnsAsync(h, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups/{groupId}/treasury/return", (HttpContext h, Guid communityId, Guid groupId, TreasuryService s, CancellationToken ct) => s.CollectAsync(h, communityId, groupId, ct));
    }
}
