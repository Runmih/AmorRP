using AmorRP.Contracts.Currency;
namespace AmorRP.Server.Features.Currency;

public static class TreasuryEndpoints
{
    public static void MapTreasuryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/treasury", (HttpContext h, Guid seasonId, Guid groupId, TreasuryService s, CancellationToken ct) => s.BalanceAsync(h, seasonId, groupId, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/records", (HttpContext h, Guid seasonId, Guid groupId, TreasuryService s, CancellationToken ct) => s.RecordsAsync(h, seasonId, groupId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/deposits", (HttpContext h, Guid seasonId, Guid groupId, TreasuryTransfer b, TreasuryService s, CancellationToken ct) => s.TransferAsync(h, seasonId, groupId, b, true, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/withdrawals", (HttpContext h, Guid seasonId, Guid groupId, TreasuryTransfer b, TreasuryService s, CancellationToken ct) => s.TransferAsync(h, seasonId, groupId, b, false, ct));
        app.MapGet("/api/v2/me/treasury-returns", (HttpContext h, TreasuryService s, CancellationToken ct) => s.ReturnsAsync(h, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/return", (HttpContext h, Guid seasonId, Guid groupId, TreasuryService s, CancellationToken ct) => s.CollectAsync(h, seasonId, groupId, ct));
    }
}
