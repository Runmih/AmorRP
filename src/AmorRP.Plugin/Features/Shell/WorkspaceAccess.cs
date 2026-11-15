using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Groups;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Shell;

public enum WorkspaceKind { Crafting, GroupManagement, CommunityManagement, Community }

internal static class WorkspaceAccess
{
    private static readonly string[] CommunityActions =
        ["community.moderate", "announcements.manage", "members.gameplay.manage", "currency.manage", "inventory.remove", "groups.manage"];

    public static bool CommunityManagement(Community? community, Guid? actor) => community != null && actor.HasValue
        && (community.OwnerCharacterId == actor || community.MyCapabilities.Any(CommunityActions.Contains));

    public static bool GroupManagement(Community? community, Guid? actor, GroupProfile group) => community != null
        && actor.HasValue && group.CommunityId == community.Id && group.IsMember
        && (group.OwnerCharacterId == actor || (group.MyCapabilities ?? []).Any(action =>
            GroupCapabilities.All.Contains(action) && GroupCapabilities.Enabled(group.EnabledFeatures ?? [], action)));

    public static bool OpenGroupManagement(Community? community, Guid? actor, IEnumerable<GroupProfile> groups) => community != null
        && actor.HasValue && (community.OwnerCharacterId == actor || community.MyCapabilities.Contains("groups.create")
            || groups.Any(group => GroupManagement(community, actor, group)));
}
