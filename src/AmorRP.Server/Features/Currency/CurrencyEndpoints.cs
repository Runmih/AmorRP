using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Communities;
namespace AmorRP.Server.Features.Currency;

public static class CurrencyEndpoints
{
    public static void MapCurrencyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/currency", (HttpContext h, Guid communityId, CommunityService s, CancellationToken ct) => s.ReadAsync(h, communityId, "currency", ct));
        app.MapPatch("/api/v2/communities/{communityId}/currency", (HttpContext h, Guid communityId, CurrencyEdit b, CommunityService s, CancellationToken ct) => s.EditAsync(h, communityId, b, "currency", ct));
        app.MapGet("/api/v2/communities/{communityId}/balances", (HttpContext h, Guid communityId, CommunityService s, CancellationToken ct) => s.ReadAsync(h, communityId, "balances", ct));
        app.MapGet("/api/v2/communities/{communityId}/members/{characterId}/balances", (HttpContext h, Guid communityId, Guid characterId, CurrencyService s, CancellationToken ct) => s.MemberBalanceAsync(h, communityId, characterId, ct));
        app.MapPost("/api/v2/communities/{communityId}/currency/adjustments", (HttpContext h, Guid communityId, CurrencyAdjustment b, CurrencyService s, CancellationToken ct) => s.AdjustAsync(h, communityId, b, ct));
    }
}
