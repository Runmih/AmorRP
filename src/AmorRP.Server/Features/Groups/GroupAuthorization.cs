using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Groups;

public static class GroupAuthorization
{
    public static string[] Effective(GroupRow group, Guid actor, GroupMembershipRow? member) =>
        member == null ? [] : GroupCapabilities.All.Where(x => GroupCapabilities.Enabled(group.EnabledFeatures, x)
            && (group.OwnerCharacterId == actor || member.Capabilities.Contains(x))).Order().ToArray();

    public static string ETag(GroupMembershipRow member) =>
        $"\"group-member:{member.GroupId:N}:{member.CharacterId:N}:{member.PermissionVersion:N}\"";

    // Callers authorize the community and hold its command lock for mutations.
    public static async Task<GroupMembershipRow> MemberAsync(AmorDbContext db, GroupRow group, Guid actor, CancellationToken ct)
    {
        var member = await db.GroupMemberships.SingleOrDefaultAsync(x => x.CommunityId == group.CommunityId && x.GroupId == group.Id && x.CharacterId == actor, ct);
        ApiFault.Require(member != null && await db.Memberships.AnyAsync(x => x.CommunityId == group.CommunityId && x.CharacterId == actor && x.Status == "active", ct),
            403, "group_membership_required", "Join this group to access its private contents and staff actions.");
        return member!;
    }

    public static async Task ActionAsync(AmorDbContext db, GroupRow group, Guid actor, string capability, CancellationToken ct)
    {
        var member = await MemberAsync(db, group, actor, ct);
        ApiFault.Require(Effective(group, actor, member).Contains(capability), 403, "group_capability_required", "You do not have permission for this group action.");
    }

    public static void Validate(string[]? values, bool features = false)
    {
        var allowed = features ? GroupCapabilities.Features : GroupCapabilities.All;
        ApiFault.Require(values != null && values.Length <= allowed.Count
        && values.Distinct(StringComparer.Ordinal).Count() == values.Length && values.All(allowed.Contains),
        422, "invalid_group_capability", "Choose only the available group permissions, without duplicates.");
    }
}
