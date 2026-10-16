using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Groups;
namespace AmorRP.Server.Features.Currency;

public static class CurrencyEndpoints
{
    public static void MapCurrencyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/groups/{groupId}/currency", (HttpContext h, Guid groupId, GroupService s, CancellationToken ct) => s.ReadAsync(h, groupId, "currency", ct));
        app.MapPatch("/api/v1/groups/{groupId}/currency", (HttpContext h, Guid groupId, CurrencyEdit b, GroupService s, CancellationToken ct) => s.EditAsync(h, groupId, b, "currency", ct));
        app.MapGet("/api/v1/groups/{groupId}/balances", (HttpContext h, Guid groupId, GroupService s, CancellationToken ct) => s.ReadAsync(h, groupId, "balances", ct));
        app.MapGet("/api/v1/groups/{groupId}/members/{characterId}/balances", (HttpContext h, Guid groupId, Guid characterId, CurrencyService s, CancellationToken ct) => s.MemberBalanceAsync(h, groupId, characterId, ct));
        app.MapPost("/api/v1/groups/{groupId}/currency/adjustments", (HttpContext h, Guid groupId, CurrencyAdjustment b, CurrencyService s, CancellationToken ct) => s.AdjustAsync(h, groupId, b, ct));
    }
}
