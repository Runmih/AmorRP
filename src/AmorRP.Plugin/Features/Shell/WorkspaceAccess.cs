using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Groups;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Shell;

public enum WorkspaceKind { Crafting, Groups, CommunityManagement, Season, Inventory, Writing }

internal static class WorkspaceAccess
{
    private static readonly string[] CommunityActions =
        ["season.moderate", "announcements.manage", "members.gameplay.manage", "currency.manage", "inventory.remove", "groups.manage"];

    public static bool CommunityManagement(Season? community, Guid? actor) => community != null && actor.HasValue
        && (community.OwnerCharacterId == actor || community.MyCapabilities.Any(CommunityActions.Contains));

    public static bool GroupManagement(Season? community, Guid? actor, GroupProfile group) => community != null
        && actor.HasValue && group.CommunityId == community.Id && group.IsMember
        && (group.OwnerCharacterId == actor || (group.MyCapabilities ?? []).Any(action =>
            GroupCapabilities.All.Contains(action) && GroupCapabilities.Enabled(group.EnabledFeatures ?? [], action)));
}
