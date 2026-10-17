using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Services.Api;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private CommandRequest? affectedRefresh;
    private void RefreshAffected(CommandRequest request)
    {
        if (session == null || origin == null) return;
        if (syncWork != null) { affectedRefresh = request; return; }
        affectedRefresh = null;
        var p = request.Path;
        if (p.Contains("/groups", StringComparison.Ordinal)) { refreshNeeded = true; return; }
        if (community == null || p is "api/v2/communities" or "api/v2/communities/join" || p.EndsWith("/deletion", StringComparison.Ordinal)
            || (request.Method == "DELETE" && p == $"api/v2/communities/{community.Id}/members/{session.Character.Id}"))
        { refreshNeeded = true; return; }
        var uri = origin; var token = session.AccessToken; var id = community.Id; var target = memberInventoryFor;
        var inventoryChanged = p.Contains("/inventory", StringComparison.Ordinal) || p.Contains("/holdings", StringComparison.Ordinal)
            || p.Contains("/letters", StringComparison.Ordinal) || p.Contains("/item-definitions", StringComparison.Ordinal) || p.Contains("/trades", StringComparison.Ordinal);
        var membersChanged = p.Contains("/members", StringComparison.Ordinal) || p.Contains("/ownership-transfers", StringComparison.Ordinal);
        var inactive = Owner && showInactive;
        StartSync(async ct => {
            var updated = await client.GetAsync<Community>(uri, $"api/v2/communities/{id}", token, ct);
            var state = inventoryChanged ? await LoadInventoryAsync(uri, token, id, ct) : null;
            var roster = membersChanged ? await client.GetAsync<MemberPage>(uri, $"api/v2/communities/{id}/members" + (inactive ? "?includeDormant=true" : ""), token, ct) : null;
            HoldingPage? targetInventory = null;
            if (inventoryChanged && target.HasValue && (Owner || updated.MyCapabilities.Contains("inventory.remove")))
                targetInventory = await client.GetAsync<HoldingPage>(uri, $"api/v2/communities/{id}/members/{target}/holdings", token, ct);
            var limits = p.EndsWith("/policies", StringComparison.Ordinal) ? await client.GetAsync<CommunityPolicies>(uri, $"api/v2/communities/{id}/policies", token, ct) : null;
            var invites = p.Contains("/invitations", StringComparison.Ordinal) ? await client.GetAsync<InvitationPage>(uri, $"api/v2/communities/{id}/invitations", token, ct) : null;
            return () => {
                community = updated; communities = communities.Select(x => x.Id == updated.Id ? updated : x).ToArray();
                if (!(updated.OwnerCharacterId == session!.Character.Id || updated.MyCapabilities.Contains("inventory.remove"))) { memberHoldings = []; memberInventoryFor = null; memberHoldingSelection = null; }
                if (state != null) AcceptInventoryState(state);
                if (roster != null) { members = roster.Items; membersCursor = roster.NextCursor; }
                if (targetInventory != null && memberInventoryFor == target) { memberHoldings = targetInventory.Items; memberHoldingCursor = targetInventory.NextCursor; }
                if (limits != null) policies = limits;
                if (invites != null) { invitations = invites.Items; invitationsCursor = invites.NextCursor; }
                if (p.EndsWith("/logo", StringComparison.Ordinal)) { logos.Clear(); refreshNeeded = true; }
                if (p.Contains("/currency/icon", StringComparison.Ordinal)) { icons.Clear(); refreshNeeded = true; }
                lastRefresh = DateTimeOffset.UtcNow;
            };
        });
    }
}
