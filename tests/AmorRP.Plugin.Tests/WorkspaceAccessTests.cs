using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Features.Shell;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Tests;

public sealed class WorkspaceAccessTests
{
    private static readonly Guid Actor = Guid.NewGuid(), Other = Guid.NewGuid(), CommunityId = Guid.NewGuid();
    private static Community Scope(params string[] capabilities) => new(CommunityId, "Community", "", Other, 1,
        new Currency(Guid.NewGuid(), CommunityId, "Coins", "C", 1), capabilities, false,
        new Balance(Guid.NewGuid(), Actor, "0", "0", "0", 1), null);
    private static GroupProfile Group(bool member = true, params string[] actions) => new(Guid.NewGuid(), CommunityId,
        "Group", "", null, 1, 1, "etag", Other, IsMember: member,
        EnabledFeatures: [..GroupCapabilities.DefaultFeatures, GroupCapabilities.Treasury], MyCapabilities: actions);

    [Theory]
    [InlineData("community.moderate")]
    [InlineData("announcements.manage")]
    [InlineData("members.gameplay.manage")]
    [InlineData("currency.manage")]
    [InlineData("inventory.remove")]
    [InlineData("groups.manage")]
    public void Relevant_delegated_community_actions_expose_the_management_workspace(string action)
    {
        Assert.True(WorkspaceAccess.CommunityManagement(Scope(action), Actor));
        Assert.False(WorkspaceAccess.GroupManagement(Scope(action), Actor, Group()));
    }

    [Theory]
    [InlineData("groups.create")]
    [InlineData("items.potion.create")]
    [InlineData("items.cooking.create")]
    [InlineData("items.potion.define")]
    [InlineData("items.cooking.define")]
    public void Crafting_and_group_creation_do_not_imply_community_moderation(string action)
        => Assert.False(WorkspaceAccess.CommunityManagement(Scope(action), Actor));

    [Fact]
    public void Community_owners_can_approve_groups_but_do_not_implicitly_gain_private_group_authority()
    {
        var owned = Scope() with { OwnerCharacterId = Actor };
        var otherGroup = Group(member: false);
        Assert.True(WorkspaceAccess.CommunityManagement(owned, Actor));
        Assert.True(WorkspaceAccess.OpenGroupManagement(owned, Actor, [otherGroup])); // They may create a group.
        Assert.False(WorkspaceAccess.GroupManagement(owned, Actor, otherGroup));
        Assert.False(WorkspaceAccess.GroupManagement(Scope("groups.manage"), Actor, Group()));
    }

    [Theory]
    [InlineData(GroupCapabilities.Billing)]
    [InlineData(GroupCapabilities.TreasuryRecords)]
    [InlineData(GroupCapabilities.TreasuryWithdraw)]
    [InlineData(GroupCapabilities.Announcements)]
    [InlineData(GroupCapabilities.Notice)]
    [InlineData(GroupCapabilities.Events)]
    public void Staff_actions_need_membership_the_matching_community_and_feature_approval(string action)
    {
        var group = Group(true, action);
        Assert.True(WorkspaceAccess.GroupManagement(Scope(), Actor, group));
        Assert.True(WorkspaceAccess.OpenGroupManagement(Scope(), Actor, [group]));
        Assert.False(WorkspaceAccess.GroupManagement(Scope(), Actor, group with { IsMember = false }));
        Assert.False(WorkspaceAccess.GroupManagement(Scope(), Actor, group with { CommunityId = Guid.NewGuid() }));
        Assert.False(WorkspaceAccess.GroupManagement(Scope(), Actor, group with { EnabledFeatures = [] }));
    }

    [Fact]
    public void Group_owner_profiles_and_staff_management_do_not_require_a_financial_feature()
    {
        var group = Group() with { OwnerCharacterId = Actor, EnabledFeatures = [] };
        Assert.True(WorkspaceAccess.GroupManagement(Scope(), Actor, group));
        Assert.False(WorkspaceAccess.GroupManagement(Scope(), Actor, group with { IsMember = false }));
    }

    [Fact]
    public void Membership_favorites_trust_and_feature_approval_do_not_grant_management()
    {
        var group = Group() with { IsFavorite = true, IsTrusted = true };
        Assert.False(WorkspaceAccess.GroupManagement(Scope(), Actor, group));
        Assert.False(WorkspaceAccess.OpenGroupManagement(Scope(), Actor, [group]));
        Assert.True(WorkspaceAccess.OpenGroupManagement(Scope("groups.create"), Actor, [group]));
        Assert.False(WorkspaceAccess.GroupManagement(Scope("groups.create"), Actor, group));
        Assert.False(WorkspaceAccess.GroupManagement(Scope(), Actor, group with { MyCapabilities = ["unknown.action"] }));
    }

    [Fact]
    public void Missing_identity_or_community_never_exposes_management_controls()
    {
        Assert.False(WorkspaceAccess.CommunityManagement(null, Actor));
        Assert.False(WorkspaceAccess.CommunityManagement(Scope(), null));
        Assert.False(WorkspaceAccess.GroupManagement(null, Actor, Group()));
        Assert.False(WorkspaceAccess.GroupManagement(Scope(), null, Group()));
        Assert.False(WorkspaceAccess.OpenGroupManagement(null, Actor, []));
        Assert.False(WorkspaceAccess.OpenGroupManagement(Scope(), null, []));
    }
}
