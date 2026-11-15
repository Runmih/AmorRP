using AmorRP.Plugin.Services.Api;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private void RefreshAffected(CommandRequest request) =>
        RefreshAffected(request.AppliesToCommunity(community?.Id) ? request.Operation.Refresh : RefreshTargets.Season);

    private void RefreshAffected(RefreshTargets targets)
    {
        if (session == null || origin == null) return;
        if (targets.HasFlag(RefreshTargets.Groups))
        { groupsRefresh.Invalidate(); hubRefresh.Invalidate(); favoriteRefresh.Invalidate(); }
        if (targets.HasFlag(RefreshTargets.Inventory))
        {
            inventoryRefresh.Invalidate(); inspectorRefresh.Invalidate(); companionInventoryRefresh.Invalidate();
            // A trade or removal can revoke access to the selected written body.
            // Require a fresh owned-item read before displaying it again.
            writingLogDetails = null; writingLogRefresh.Invalidate();
        }
        if (targets.HasFlag(RefreshTargets.Members)) { rosterRefresh.Invalidate(); ownershipRefresh.Invalidate(); }
        if (targets.HasFlag(RefreshTargets.Policy)) policyRefresh.Invalidate();
        if (targets.HasFlag(RefreshTargets.CommunityLogo)) { logos.Clear(); logoReadKey = ""; }
        if (targets.HasFlag(RefreshTargets.CurrencyIcon)) { icons.Clear(); iconReadKey = ""; }
        if (targets.HasFlag(RefreshTargets.Season)) { refreshNeeded = true; myBillsRefresh.Invalidate(); }
    }
}
