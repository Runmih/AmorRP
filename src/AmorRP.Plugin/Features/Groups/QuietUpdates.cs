using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Services.Api;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private CommandRequest? affectedRefresh;
    private void RefreshAffected(CommandRequest request)
    {
        if (session == null || origin == null) return;
        if (syncWork != null) { affectedRefresh = request; return; }
        affectedRefresh = null;
        var p = request.Path;
        if (group == null || p is "api/v1/groups" or "api/v1/groups/join" || p.EndsWith("/deletion", StringComparison.Ordinal)
            || (request.Method == "DELETE" && p == $"api/v1/groups/{group.Id}/members/{session.Character.Id}"))
        { refreshNeeded = true; return; }
        var uri = origin; var token = session.AccessToken; var id = group.Id; var target = memberInventoryFor;
        var inventoryChanged = p.Contains("/inventory", StringComparison.Ordinal) || p.Contains("/holdings", StringComparison.Ordinal)
            || p.Contains("/letters", StringComparison.Ordinal) || p.Contains("/item-definitions", StringComparison.Ordinal) || p.Contains("/trades", StringComparison.Ordinal);
        var membersChanged = p.Contains("/members", StringComparison.Ordinal) || p.Contains("/ownership-transfers", StringComparison.Ordinal);
        var inactive = Owner && showInactive;
        StartSync(async ct => {
            var updated = await client.GetAsync<Group>(uri, $"api/v1/groups/{id}", token, ct);
            var state = inventoryChanged ? await LoadInventoryAsync(uri, token, id, ct) : null;
            var roster = membersChanged ? await client.GetAsync<MemberPage>(uri, $"api/v1/groups/{id}/members" + (inactive ? "?includeDormant=true" : ""), token, ct) : null;
            HoldingPage? targetInventory = null;
            if (inventoryChanged && target.HasValue && (Owner || updated.MyCapabilities.Contains("inventory.remove")))
                targetInventory = await client.GetAsync<HoldingPage>(uri, $"api/v1/groups/{id}/members/{target}/holdings", token, ct);
            var limits = p.EndsWith("/policies", StringComparison.Ordinal) ? await client.GetAsync<GroupPolicies>(uri, $"api/v1/groups/{id}/policies", token, ct) : null;
            var invites = p.Contains("/invitations", StringComparison.Ordinal) ? await client.GetAsync<InvitationPage>(uri, $"api/v1/groups/{id}/invitations", token, ct) : null;
            return () => {
                group = updated; groups = groups.Select(x => x.Id == updated.Id ? updated : x).ToArray();
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
