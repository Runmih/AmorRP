using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
namespace AmorRP.Server.Features.Seasons;

public static class SeasonResourceEndpoints
{
    public static void MapSeasonResourceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/members/{characterId}", (HttpContext h, Guid seasonId, Guid characterId, SeasonResourceService s, CancellationToken ct) => s.MemberAsync(h, seasonId, characterId, ct));
        app.MapPatch("/api/v2/seasons/{seasonId}/members/{characterId}/description", (HttpContext h, Guid seasonId, Guid characterId, MemberDescriptionEdit b, SeasonResourceService s, CancellationToken ct) => s.MemberDescriptionAsync(h, seasonId, characterId, b, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/members/{characterId}", (HttpContext h, Guid seasonId, Guid characterId, [Microsoft.AspNetCore.Mvc.FromBody] MemberRemoval b, SeasonResourceService s, CancellationToken ct) => s.MembershipAsync(h, seasonId, characterId, b, "remove", ct));
        app.MapPut("/api/v2/seasons/{seasonId}/members/{characterId}/block", (HttpContext h, Guid seasonId, Guid characterId, MemberRemoval b, SeasonResourceService s, CancellationToken ct) => s.MembershipAsync(h, seasonId, characterId, b, "block", ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/members/{characterId}/block", (HttpContext h, Guid seasonId, Guid characterId, [Microsoft.AspNetCore.Mvc.FromBody] MemberRemoval b, SeasonResourceService s, CancellationToken ct) => s.MembershipAsync(h, seasonId, characterId, b, "unblock", ct));
        app.MapGet("/api/v2/seasons", (HttpContext h, SeasonResourceService s, CancellationToken ct) => s.ListAsync(h, ct));
        app.MapPost("/api/v2/seasons", (HttpContext h, SeasonCreate b, SeasonResourceService s, CancellationToken ct) => s.CreateAsync(h, b, ct));
        app.MapGet("/api/v2/seasons/{seasonId}", (HttpContext h, Guid seasonId, SeasonResourceService s, CancellationToken ct) => s.ReadAsync(h, seasonId, "season", ct));
        app.MapPatch("/api/v2/seasons/{seasonId}", (HttpContext h, Guid seasonId, SeasonEdit b, SeasonResourceService s, CancellationToken ct) => s.EditAsync(h, seasonId, b, "edit", ct));
        app.MapGet("/api/v2/seasons/{seasonId}/policies", (HttpContext h, Guid seasonId, SeasonResourceService s, CancellationToken ct) => s.ReadAsync(h, seasonId, "policies", ct));
        app.MapPut("/api/v2/seasons/{seasonId}/policies", (HttpContext h, Guid seasonId, PolicyEdit b, SeasonResourceService s, CancellationToken ct) => s.EditAsync(h, seasonId, b, "policies", ct));
        app.MapGet("/api/v2/seasons/{seasonId}/members", (HttpContext h, Guid seasonId, SeasonResourceService s, CancellationToken ct) => s.MembersAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/members/{characterId}/capabilities", (HttpContext h, Guid seasonId, Guid characterId, MemberCapabilities b, SeasonResourceService s, CancellationToken ct) => s.MemberCommandAsync(h, seasonId, characterId, b, "capabilities", ct));
        app.MapPut("/api/v2/seasons/{seasonId}/members/{characterId}/trade-restriction", (HttpContext h, Guid seasonId, Guid characterId, MemberRestriction b, SeasonResourceService s, CancellationToken ct) => s.MemberCommandAsync(h, seasonId, characterId, b, "restriction", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/members/{characterId}/removal", (HttpContext h, Guid seasonId, Guid characterId, MemberRemoval b, SeasonResourceService s, CancellationToken ct) => s.MemberCommandAsync(h, seasonId, characterId, b, "remove", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/members/{characterId}/restoration", (HttpContext h, Guid seasonId, Guid characterId, MemberRemoval b, SeasonResourceService s, CancellationToken ct) => s.MemberCommandAsync(h, seasonId, characterId, b, "restore", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/ownership-transfers", (HttpContext h, Guid seasonId, OwnershipProposal b, SeasonResourceService s, CancellationToken ct) => s.OwnershipAsync(h, seasonId, null, b, "propose", ct));
        app.MapGet("/api/v2/seasons/{seasonId}/ownership-transfers/{transferId}", (HttpContext h, Guid seasonId, Guid transferId, SeasonResourceService s, CancellationToken ct) => s.OwnershipAsync(h, seasonId, transferId, null, "read", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/ownership-transfers/{transferId}/accept", (HttpContext h, Guid seasonId, Guid transferId, SeasonResourceService s, CancellationToken ct) => s.OwnershipAsync(h, seasonId, transferId, null, "accept", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/ownership-transfers/{transferId}/cancel", (HttpContext h, Guid seasonId, Guid transferId, SeasonResourceService s, CancellationToken ct) => s.OwnershipAsync(h, seasonId, transferId, null, "cancel", ct));
    }
}
