using AmorRP.Plugin.Services.Api;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private void RefreshAffected(CommandRequest request)
    {
        if (session == null || origin == null) return;
        var p = request.Path;
        if (p.Contains("/groups", StringComparison.Ordinal))
        {
            groupsRefresh.Invalidate(); hubRefresh.Invalidate(); favoriteRefresh.Invalidate();
            return;
        }
        if (community == null || p is "api/v2/communities" or "api/v2/communities/join" || p.EndsWith("/deletion", StringComparison.Ordinal)
            || (request.Method == "DELETE" && p == $"api/v2/communities/{community.Id}/members/{session.Character.Id}"))
        { refreshNeeded = true; return; }
        var inventoryChanged = p.Contains("/inventory", StringComparison.Ordinal) || p.Contains("/holdings", StringComparison.Ordinal)
            || p.Contains("/letters", StringComparison.Ordinal) || p.Contains("/item-definitions", StringComparison.Ordinal) || p.Contains("/trades", StringComparison.Ordinal);
        var membersChanged = p.Contains("/members", StringComparison.Ordinal) || p.Contains("/ownership-transfers", StringComparison.Ordinal);
        if (inventoryChanged) { inventoryRefresh.Invalidate(); inspectorRefresh.Invalidate(); }
        if (membersChanged) { rosterRefresh.Invalidate(); ownershipRefresh.Invalidate(); }
        if (p.EndsWith("/policies", StringComparison.Ordinal)) policyRefresh.Invalidate();
        if (p.Contains("/invitations", StringComparison.Ordinal)) invitationRefresh.Invalidate();
        if (p.EndsWith("/logo", StringComparison.Ordinal)) { logos.Clear(); logoReadKey = ""; }
        if (p.Contains("/currency/icon", StringComparison.Ordinal)) { icons.Clear(); iconReadKey = ""; }
        if (request.Method != "GET") refreshNeeded = true;
    }
}
