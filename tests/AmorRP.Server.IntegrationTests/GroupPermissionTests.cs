using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private static async Task<GroupMember> StaffRow(HttpClient owner, string path, Guid actor) =>
        (await owner.GetFromJsonAsync<GroupPrivate>(path + "/private", TestContext.Current.CancellationToken))!.Members.Single(x => x.CharacterId == actor);

    private async Task<GroupMember> GrantStaff(HttpClient owner, string path, Guid actor, params string[] actions)
    {
        var member = await StaffRow(owner, path, actor);
        return await Command<GroupMember>(owner, HttpMethod.Put, path + "/members/" + actor + "/capabilities", new GroupStaffEdit(actions), member.Etag);
    }

    [Fact]
    public async Task Group_owner_can_delegate_existing_content_without_granting_ownership_or_community_powers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory);
        using var communityOwner = factory.CreateClient(); await Login(communityOwner, "1100"); var community = await Create(communityOwner);
        using var owner = factory.CreateClient(); var ownerId = (await Login(owner, "1101")).Character.Id;
        var invitation = await Invite(communityOwner, community); await Join(owner, invitation);
        await GrantHome(communityOwner, community, ownerId, "groups.create");
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/groups", new GroupWrite("Delegated", ""));
        var path = $"/api/v2/communities/{community.Id}/groups/{group.Id}";
        Assert.Equal(GroupCapabilities.DefaultFeatures.Order(), group.EnabledFeatures!.Order());
        Assert.Equal(GroupCapabilities.DefaultFeatures.Order(), group.MyCapabilities!.Order());
        using var staff = factory.CreateClient(); var staffId = (await Login(staff, "1102")).Character.Id; await Join(staff, invitation);
        await GroupAction(staff, HttpMethod.Post, path + "/join", new GroupJoin());
        await GrantStaff(owner, path, staffId, GroupCapabilities.DefaultFeatures.ToArray());
        var view = (await staff.GetFromJsonAsync<GroupProfile>(path, ct))!;
        Assert.Equal(GroupCapabilities.DefaultFeatures.Order(), view.MyCapabilities!.Order());
        await GroupAction(staff, HttpMethod.Post, path + "/announcements", new AnnouncementWrite("Staff news", "Private message"));
        await GroupAction(staff, HttpMethod.Put, path + "/notice", new NoticeWrite("Request", "Staff can post", "Coins"));
        var start = clock.GetUtcNow().AddDays(1);
        await GroupAction(staff, HttpMethod.Post, path + "/events", new EventWrite("Staff event", "Details", "Limsa", start, start.AddHours(1)));
        Assert.Single((await owner.GetFromJsonAsync<GroupPrivate>(path + "/private", ct))!.Announcements);
        Assert.Null((await staff.GetFromJsonAsync<GroupPrivate>(path + "/private", ct))!.Members.Single(x => x.CharacterId == staffId).Capabilities);
        using var edit = Request(HttpMethod.Patch, path, new GroupWrite("Override", ""), etag: view.Etag);
        using var deniedEdit = await staff.SendAsync(edit, ct); Assert.Equal(HttpStatusCode.Forbidden, deniedEdit.StatusCode);
        using var grant = Request(HttpMethod.Put, path + "/members/" + staffId + "/capabilities", new GroupStaffEdit([]), etag: "\"fake\"");
        using var deniedGrant = await staff.SendAsync(grant, ct); Assert.Equal(HttpStatusCode.Forbidden, deniedGrant.StatusCode);
        using var approval = Request(HttpMethod.Put, path + "/features", new GroupFeaturesEdit([]), etag: group.Etag);
        using var deniedApproval = await owner.SendAsync(approval, ct); Assert.Equal(HttpStatusCode.Forbidden, deniedApproval.StatusCode);
        using var privateDenied = await communityOwner.GetAsync(path + "/private", ct); Assert.Equal(HttpStatusCode.Forbidden, privateDenied.StatusCode);
        Assert.Empty((await staff.GetFromJsonAsync<Community>($"/api/v2/communities/{community.Id}", ct))!.MyCapabilities);
    }

    [Fact]
    public async Task Feature_revocation_stops_owner_and_staff_actions_and_does_not_restore_old_grants_when_reenabled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "1110"); var community = await Create(owner);
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/groups", new GroupWrite("Features", ""));
        var path = $"/api/v2/communities/{community.Id}/groups/{group.Id}";
        using var staff = factory.CreateClient(); var id = (await Login(staff, "1111")).Character.Id; await Join(staff, await Invite(owner, community));
        await GroupAction(staff, HttpMethod.Post, path + "/join", new GroupJoin()); await GrantStaff(owner, path, id, GroupCapabilities.Notice);
        var key = Guid.CreateVersion7(); var body = new NoticeWrite("Notice", "Keep original", "");
        using (var request = Request(HttpMethod.Put, path + "/notice", body, key))
        using (var posted = await staff.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        group = await Command<GroupProfile>(owner, HttpMethod.Put, path + "/features", new GroupFeaturesEdit([GroupCapabilities.Events]), group.Etag);
        foreach (var client in new[] { owner, staff })
        {
            using var request = Request(HttpMethod.Put, path + "/notice", body, key);
            using var denied = await client.SendAsync(request, ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        Assert.Empty((await StaffRow(owner, path, id)).Capabilities!);
        group = await Command<GroupProfile>(owner, HttpMethod.Put, path + "/features", new GroupFeaturesEdit(GroupCapabilities.DefaultFeatures.ToArray()), group.Etag);
        Assert.Empty((await staff.GetFromJsonAsync<GroupProfile>(path, ct))!.MyCapabilities!);
        var start = clock.GetUtcNow().AddDays(1);
        await GroupAction(owner, HttpMethod.Post, path + "/events", new EventWrite("Owner event", "Still enabled", "Limsa", start, start.AddHours(1)));
    }

    [Fact]
    public async Task Grants_are_specific_to_the_group_and_known_enabled_actions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "1120"); var community = await Create(owner);
        var root = $"/api/v2/communities/{community.Id}/groups";
        var a = await Command<GroupProfile>(owner, HttpMethod.Post, root, new GroupWrite("A", ""));
        var b = await Command<GroupProfile>(owner, HttpMethod.Post, root, new GroupWrite("B", ""));
        using var staff = factory.CreateClient(); var id = (await Login(staff, "1121")).Character.Id; await Join(staff, await Invite(owner, community));
        foreach (var group in new[] { a, b }) await GroupAction(staff, HttpMethod.Post, root + "/" + group.Id + "/join", new GroupJoin());
        await GrantStaff(owner, root + "/" + a.Id, id, GroupCapabilities.Announcements);
        foreach (var suffix in new[] { "/notice", "/events" })
        {
            object body = suffix == "/notice" ? new NoticeWrite("Denied", "Denied", "") : new EventWrite("Denied", "Denied", "Limsa", clock.GetUtcNow().AddDays(1), clock.GetUtcNow().AddDays(1).AddHours(1));
            using var request = Request(suffix == "/notice" ? HttpMethod.Put : HttpMethod.Post, root + "/" + a.Id + suffix, body);
            using var denied = await staff.SendAsync(request, ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var other = Request(HttpMethod.Post, root + "/" + b.Id + "/announcements", new AnnouncementWrite("Denied", "Other group"));
        using var deniedOther = await staff.SendAsync(other, ct); Assert.Equal(HttpStatusCode.Forbidden, deniedOther.StatusCode);
        var member = await StaffRow(owner, root + "/" + a.Id, id);
        using var escalation = Request(HttpMethod.Put, root + "/" + a.Id + "/members/" + id + "/capabilities", new GroupStaffEdit(["currency.manage"]), etag: member.Etag);
        using var deniedEscalation = await owner.SendAsync(escalation, ct); Assert.Equal(HttpStatusCode.UnprocessableEntity, deniedEscalation.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Leaving_a_group_or_community_clears_staff_grants_before_rejoining(bool leaveCommunity)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "1130"); var community = await Create(owner);
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/groups", new GroupWrite("Membership", ""));
        var path = $"/api/v2/communities/{community.Id}/groups/{group.Id}";
        using var staff = factory.CreateClient(); var id = (await Login(staff, "1131")).Character.Id; var invitation = await Invite(owner, community); await Join(staff, invitation);
        await GroupAction(staff, HttpMethod.Post, path + "/join", new GroupJoin()); var before = await GrantStaff(owner, path, id, GroupCapabilities.Events);
        if (leaveCommunity)
        {
            var member = await Member(owner, community, id);
            await Command<Member>(owner, HttpMethod.Delete, $"/api/v2/communities/{community.Id}/members/{id}", new MemberRemoval("Removed"), member.Etag);
            await Join(staff, invitation);
        }
        else
        {
            await GroupAction(staff, HttpMethod.Delete, path + "/membership");
            await GroupAction(staff, HttpMethod.Post, path + "/join", new GroupJoin());
        }
        Assert.Empty((await staff.GetFromJsonAsync<GroupProfile>(path, ct))!.MyCapabilities!);
        using var stale = Request(HttpMethod.Put, path + "/members/" + id + "/capabilities", new GroupStaffEdit([GroupCapabilities.Events]), etag: before.Etag);
        using var denied = await owner.SendAsync(stale, ct); Assert.Equal(HttpStatusCode.PreconditionFailed, denied.StatusCode);
    }

    [Fact]
    public async Task Staff_permission_preconditions_and_retries_preserve_the_committed_grant()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "1140"); var community = await Create(owner);
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/groups", new GroupWrite("Receipts", ""));
        var path = $"/api/v2/communities/{community.Id}/groups/{group.Id}";
        using var staff = factory.CreateClient(); var id = (await Login(staff, "1141")).Character.Id; await Join(staff, await Invite(owner, community));
        await GroupAction(staff, HttpMethod.Post, path + "/join", new GroupJoin()); var before = await StaffRow(owner, path, id);
        var key = Guid.CreateVersion7(); var endpoint = path + "/members/" + id + "/capabilities";
        for (var i = 0; i < 2; i++)
        {
            using var request = Request(HttpMethod.Put, endpoint, new GroupStaffEdit([GroupCapabilities.Events]), key, before.Etag);
            using var reply = await owner.SendAsync(request, ct); Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
            Assert.Equal(i == 1, reply.Headers.Contains("Idempotency-Replayed"));
        }
        using var old = Request(HttpMethod.Put, endpoint, new GroupStaffEdit([GroupCapabilities.Notice]), etag: before.Etag);
        using var rejected = await owner.SendAsync(old, ct); Assert.Equal(HttpStatusCode.PreconditionFailed, rejected.StatusCode);
        Assert.Equal(new[] { GroupCapabilities.Events }, (await StaffRow(owner, path, id)).Capabilities!);
    }

    [Fact]
    public async Task Permission_migration_preserves_existing_groups_and_enables_their_existing_owner_features()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "1150"); var community = await Create(owner);
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/groups", new GroupWrite("Existing", "Retain profile"));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261017154807_CommunityGroupActivitiesAndCrafting", ct);
        db.ChangeTracker.Clear(); await db.Database.MigrateAsync(ct);
        var restored = await db.Groups.SingleAsync(ct);
        Assert.Equal(group.Id, restored.Id); Assert.Equal(group.Description, restored.Description);
        Assert.Equal(GroupCapabilities.DefaultFeatures.Order(), restored.EnabledFeatures.Order());
        var member = await db.GroupMemberships.SingleAsync(ct); Assert.Empty(member.Capabilities); Assert.NotEqual(Guid.Empty, member.PermissionVersion);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
