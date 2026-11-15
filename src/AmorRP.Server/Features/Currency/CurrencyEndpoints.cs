using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Seasons;
namespace AmorRP.Server.Features.Currency;

public static class CurrencyEndpoints
{
    public static void MapCurrencyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/currency", (HttpContext h, Guid seasonId, SeasonResourceService s, CancellationToken ct) => s.ReadAsync(h, seasonId, "currency", ct));
        app.MapPatch("/api/v2/seasons/{seasonId}/currency", (HttpContext h, Guid seasonId, CurrencyEdit b, SeasonResourceService s, CancellationToken ct) => s.EditAsync(h, seasonId, b, "currency", ct));
        app.MapGet("/api/v2/seasons/{seasonId}/balances", (HttpContext h, Guid seasonId, SeasonResourceService s, CancellationToken ct) => s.ReadAsync(h, seasonId, "balances", ct));
        app.MapGet("/api/v2/seasons/{seasonId}/members/{characterId}/balances", (HttpContext h, Guid seasonId, Guid characterId, CurrencyService s, CancellationToken ct) => s.MemberBalanceAsync(h, seasonId, characterId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/currency/adjustments", (HttpContext h, Guid seasonId, CurrencyAdjustment b, CurrencyService s, CancellationToken ct) => s.AdjustAsync(h, seasonId, b, ct));
    }
}
