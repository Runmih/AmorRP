using AmorRP.Plugin.Services.Api;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private void RefreshAffected(CommandRequest request) => RefreshAffected(request.Operation.Refresh);

    private void RefreshAffected(RefreshTargets targets)
    {
        if (session == null || origin == null) return;
        if (targets.HasFlag(RefreshTargets.Groups))
        { groupsRefresh.Invalidate(); hubRefresh.Invalidate(); favoriteRefresh.Invalidate(); }
        if (targets.HasFlag(RefreshTargets.Inventory)) { inventoryRefresh.Invalidate(); inspectorRefresh.Invalidate(); }
        if (targets.HasFlag(RefreshTargets.Members)) { rosterRefresh.Invalidate(); ownershipRefresh.Invalidate(); }
        if (targets.HasFlag(RefreshTargets.Policy)) policyRefresh.Invalidate();
        if (targets.HasFlag(RefreshTargets.Invitations)) invitationRefresh.Invalidate();
        if (targets.HasFlag(RefreshTargets.CommunityLogo)) { logos.Clear(); logoReadKey = ""; }
        if (targets.HasFlag(RefreshTargets.CurrencyIcon)) { icons.Clear(); iconReadKey = ""; }
        if (targets.HasFlag(RefreshTargets.Community)) refreshNeeded = true;
    }
}
