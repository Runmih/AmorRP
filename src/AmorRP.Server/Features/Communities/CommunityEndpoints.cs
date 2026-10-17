using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
namespace AmorRP.Server.Features.Communities;

public static class CommunityEndpoints
{
    public static void MapCommunityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/members/{characterId}", (HttpContext h, Guid communityId, Guid characterId, CommunityService s, CancellationToken ct) => s.MemberAsync(h, communityId, characterId, ct));
        app.MapPatch("/api/v2/communities/{communityId}/members/{characterId}/description", (HttpContext h, Guid communityId, Guid characterId, MemberDescriptionEdit b, CommunityService s, CancellationToken ct) => s.MemberDescriptionAsync(h, communityId, characterId, b, ct));
        app.MapDelete("/api/v2/communities/{communityId}/members/{characterId}", (HttpContext h, Guid communityId, Guid characterId, [Microsoft.AspNetCore.Mvc.FromBody] MemberRemoval b, CommunityService s, CancellationToken ct) => s.MembershipAsync(h, communityId, characterId, b, "remove", ct));
        app.MapPut("/api/v2/communities/{communityId}/members/{characterId}/block", (HttpContext h, Guid communityId, Guid characterId, MemberRemoval b, CommunityService s, CancellationToken ct) => s.MembershipAsync(h, communityId, characterId, b, "block", ct));
        app.MapDelete("/api/v2/communities/{communityId}/members/{characterId}/block", (HttpContext h, Guid communityId, Guid characterId, [Microsoft.AspNetCore.Mvc.FromBody] MemberRemoval b, CommunityService s, CancellationToken ct) => s.MembershipAsync(h, communityId, characterId, b, "unblock", ct));
        app.MapGet("/api/v2/communities", (HttpContext h, CommunityService s, CancellationToken ct) => s.ListAsync(h, ct));
        app.MapPost("/api/v2/communities", (HttpContext h, CommunityCreate b, CommunityService s, CancellationToken ct) => s.CreateAsync(h, b, ct));
        app.MapPost("/api/v2/communities/join", (HttpContext h, JoinCommunity b, CommunityService s, CancellationToken ct) => s.JoinAsync(h, b, ct)).RequireRateLimiting("identity-probe");
        app.MapGet("/api/v2/communities/{communityId}", (HttpContext h, Guid communityId, CommunityService s, CancellationToken ct) => s.ReadAsync(h, communityId, "community", ct));
        app.MapPatch("/api/v2/communities/{communityId}", (HttpContext h, Guid communityId, CommunityEdit b, CommunityService s, CancellationToken ct) => s.EditAsync(h, communityId, b, "edit", ct));
        app.MapPost("/api/v2/communities/{communityId}/leave", (HttpContext h, Guid communityId, CommunityService s, CancellationToken ct) => s.EditAsync(h, communityId, null, "leave", ct));
        app.MapPost("/api/v2/communities/{communityId}/deletion", (HttpContext h, Guid communityId, CommunityDeletion b, CommunityService s, CancellationToken ct) => s.EditAsync(h, communityId, b, "delete", ct));
        app.MapGet("/api/v2/communities/{communityId}/policies", (HttpContext h, Guid communityId, CommunityService s, CancellationToken ct) => s.ReadAsync(h, communityId, "policies", ct));
        app.MapPut("/api/v2/communities/{communityId}/policies", (HttpContext h, Guid communityId, PolicyEdit b, CommunityService s, CancellationToken ct) => s.EditAsync(h, communityId, b, "policies", ct));
        app.MapGet("/api/v2/communities/{communityId}/members", (HttpContext h, Guid communityId, CommunityService s, CancellationToken ct) => s.MembersAsync(h, communityId, ct));
        app.MapPut("/api/v2/communities/{communityId}/members/{characterId}/capabilities", (HttpContext h, Guid communityId, Guid characterId, MemberCapabilities b, CommunityService s, CancellationToken ct) => s.MemberCommandAsync(h, communityId, characterId, b, "capabilities", ct));
        app.MapPut("/api/v2/communities/{communityId}/members/{characterId}/trade-restriction", (HttpContext h, Guid communityId, Guid characterId, MemberRestriction b, CommunityService s, CancellationToken ct) => s.MemberCommandAsync(h, communityId, characterId, b, "restriction", ct));
        app.MapPost("/api/v2/communities/{communityId}/members/{characterId}/removal", (HttpContext h, Guid communityId, Guid characterId, MemberRemoval b, CommunityService s, CancellationToken ct) => s.MemberCommandAsync(h, communityId, characterId, b, "remove", ct));
        app.MapPost("/api/v2/communities/{communityId}/members/{characterId}/restoration", (HttpContext h, Guid communityId, Guid characterId, MemberRemoval b, CommunityService s, CancellationToken ct) => s.MemberCommandAsync(h, communityId, characterId, b, "restore", ct));
        app.MapGet("/api/v2/communities/{communityId}/invitations", (HttpContext h, Guid communityId, CommunityService s, CancellationToken ct) => s.InvitationsAsync(h, communityId, ct));
        app.MapPost("/api/v2/communities/{communityId}/invitations", (HttpContext h, Guid communityId, InvitationCreate b, CommunityService s, CancellationToken ct) => s.InvitationCommandAsync(h, communityId, null, b, ct));
        app.MapDelete("/api/v2/communities/{communityId}/invitations/{invitationId}", (HttpContext h, Guid communityId, Guid invitationId, CommunityService s, CancellationToken ct) => s.InvitationCommandAsync(h, communityId, invitationId, null, ct));
        app.MapPost("/api/v2/communities/{communityId}/ownership-transfers", (HttpContext h, Guid communityId, OwnershipProposal b, CommunityService s, CancellationToken ct) => s.OwnershipAsync(h, communityId, null, b, "propose", ct));
        app.MapGet("/api/v2/communities/{communityId}/ownership-transfers/{transferId}", (HttpContext h, Guid communityId, Guid transferId, CommunityService s, CancellationToken ct) => s.OwnershipAsync(h, communityId, transferId, null, "read", ct));
        app.MapPost("/api/v2/communities/{communityId}/ownership-transfers/{transferId}/accept", (HttpContext h, Guid communityId, Guid transferId, CommunityService s, CancellationToken ct) => s.OwnershipAsync(h, communityId, transferId, null, "accept", ct));
        app.MapPost("/api/v2/communities/{communityId}/ownership-transfers/{transferId}/cancel", (HttpContext h, Guid communityId, Guid transferId, CommunityService s, CancellationToken ct) => s.OwnershipAsync(h, communityId, transferId, null, "cancel", ct));
    }
}
