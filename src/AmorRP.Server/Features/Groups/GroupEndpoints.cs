using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
namespace AmorRP.Server.Features.Groups;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/groups/{groupId}/members/{characterId}", (HttpContext h, Guid groupId, Guid characterId, GroupService s, CancellationToken ct) => s.MemberAsync(h, groupId, characterId, ct));
        app.MapPatch("/api/v1/groups/{groupId}/members/{characterId}/description", (HttpContext h, Guid groupId, Guid characterId, MemberDescriptionEdit b, GroupService s, CancellationToken ct) => s.MemberDescriptionAsync(h, groupId, characterId, b, ct));
        app.MapDelete("/api/v1/groups/{groupId}/members/{characterId}", (HttpContext h, Guid groupId, Guid characterId, [Microsoft.AspNetCore.Mvc.FromBody] MemberRemoval b, GroupService s, CancellationToken ct) => s.MembershipAsync(h, groupId, characterId, b, "remove", ct));
        app.MapPut("/api/v1/groups/{groupId}/members/{characterId}/block", (HttpContext h, Guid groupId, Guid characterId, MemberRemoval b, GroupService s, CancellationToken ct) => s.MembershipAsync(h, groupId, characterId, b, "block", ct));
        app.MapDelete("/api/v1/groups/{groupId}/members/{characterId}/block", (HttpContext h, Guid groupId, Guid characterId, [Microsoft.AspNetCore.Mvc.FromBody] MemberRemoval b, GroupService s, CancellationToken ct) => s.MembershipAsync(h, groupId, characterId, b, "unblock", ct));
        app.MapGet("/api/v1/groups", (HttpContext h, GroupService s, CancellationToken ct) => s.ListAsync(h, ct));
        app.MapPost("/api/v1/groups", (HttpContext h, GroupCreate b, GroupService s, CancellationToken ct) => s.CreateAsync(h, b, ct));
        app.MapPost("/api/v1/groups/join", (HttpContext h, JoinGroup b, GroupService s, CancellationToken ct) => s.JoinAsync(h, b, ct));
        app.MapGet("/api/v1/groups/{groupId}", (HttpContext h, Guid groupId, GroupService s, CancellationToken ct) => s.ReadAsync(h, groupId, "group", ct));
        app.MapPatch("/api/v1/groups/{groupId}", (HttpContext h, Guid groupId, GroupEdit b, GroupService s, CancellationToken ct) => s.EditAsync(h, groupId, b, "edit", ct));
        app.MapPost("/api/v1/groups/{groupId}/leave", (HttpContext h, Guid groupId, GroupService s, CancellationToken ct) => s.EditAsync(h, groupId, null, "leave", ct));
        app.MapPost("/api/v1/groups/{groupId}/deletion", (HttpContext h, Guid groupId, GroupDeletion b, GroupService s, CancellationToken ct) => s.EditAsync(h, groupId, b, "delete", ct));
        app.MapGet("/api/v1/groups/{groupId}/policies", (HttpContext h, Guid groupId, GroupService s, CancellationToken ct) => s.ReadAsync(h, groupId, "policies", ct));
        app.MapPut("/api/v1/groups/{groupId}/policies", (HttpContext h, Guid groupId, PolicyEdit b, GroupService s, CancellationToken ct) => s.EditAsync(h, groupId, b, "policies", ct));
        app.MapGet("/api/v1/groups/{groupId}/members", (HttpContext h, Guid groupId, GroupService s, CancellationToken ct) => s.MembersAsync(h, groupId, ct));
        app.MapPut("/api/v1/groups/{groupId}/members/{characterId}/capabilities", (HttpContext h, Guid groupId, Guid characterId, MemberCapabilities b, GroupService s, CancellationToken ct) => s.MemberCommandAsync(h, groupId, characterId, b, "capabilities", ct));
        app.MapPut("/api/v1/groups/{groupId}/members/{characterId}/trade-restriction", (HttpContext h, Guid groupId, Guid characterId, MemberRestriction b, GroupService s, CancellationToken ct) => s.MemberCommandAsync(h, groupId, characterId, b, "restriction", ct));
        app.MapPost("/api/v1/groups/{groupId}/members/{characterId}/removal", (HttpContext h, Guid groupId, Guid characterId, MemberRemoval b, GroupService s, CancellationToken ct) => s.MemberCommandAsync(h, groupId, characterId, b, "remove", ct));
        app.MapPost("/api/v1/groups/{groupId}/members/{characterId}/restoration", (HttpContext h, Guid groupId, Guid characterId, MemberRemoval b, GroupService s, CancellationToken ct) => s.MemberCommandAsync(h, groupId, characterId, b, "restore", ct));
        app.MapGet("/api/v1/groups/{groupId}/invitations", (HttpContext h, Guid groupId, GroupService s, CancellationToken ct) => s.InvitationsAsync(h, groupId, ct));
        app.MapPost("/api/v1/groups/{groupId}/invitations", (HttpContext h, Guid groupId, InvitationCreate b, GroupService s, CancellationToken ct) => s.InvitationCommandAsync(h, groupId, null, b, ct));
        app.MapDelete("/api/v1/groups/{groupId}/invitations/{invitationId}", (HttpContext h, Guid groupId, Guid invitationId, GroupService s, CancellationToken ct) => s.InvitationCommandAsync(h, groupId, invitationId, null, ct));
        app.MapPost("/api/v1/groups/{groupId}/ownership-transfers", (HttpContext h, Guid groupId, OwnershipProposal b, GroupService s, CancellationToken ct) => s.OwnershipAsync(h, groupId, null, b, "propose", ct));
        app.MapGet("/api/v1/groups/{groupId}/ownership-transfers/{transferId}", (HttpContext h, Guid groupId, Guid transferId, GroupService s, CancellationToken ct) => s.OwnershipAsync(h, groupId, transferId, null, "read", ct));
        app.MapPost("/api/v1/groups/{groupId}/ownership-transfers/{transferId}/accept", (HttpContext h, Guid groupId, Guid transferId, GroupService s, CancellationToken ct) => s.OwnershipAsync(h, groupId, transferId, null, "accept", ct));
        app.MapPost("/api/v1/groups/{groupId}/ownership-transfers/{transferId}/cancel", (HttpContext h, Guid groupId, Guid transferId, GroupService s, CancellationToken ct) => s.OwnershipAsync(h, groupId, transferId, null, "cancel", ct));
    }
}
