using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Community_delegation_controls_creation_and_deletion_without_overriding_group_ownership()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "910"); var community = await Create(owner);
        using var member = f.CreateClient(); var identity = await Login(member, "911"); await Enroll(member, community);
        var root = $"/api/v2/seasons/{community.Id}/groups"; var body = new GroupWrite("City Watch", "Guarding the community.");
        using (var request = Request(HttpMethod.Post, root, body))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var current = await GrantHome(owner, community, identity.Character.Id, "groups.create");
        var created = await Command<GroupProfile>(member, HttpMethod.Post, root, body);
        Assert.Equal(community.Id, created.CommunityId); Assert.Equal(body.Description, created.Description);
        Assert.Equal(identity.Character.Id, created.OwnerCharacterId);
        // Creation grants ownership; community moderation does not grant editing.
        using (var request = Request(HttpMethod.Patch, root + "/" + created.Id, body, etag: created.Etag))
        using (var denied = await owner.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        // Managing gameplay permissions must not allow self-granting moderator rights.
        current = await GrantHome(owner, community, identity.Character.Id, "members.gameplay.manage");
        using (var request = Request(HttpMethod.Put, $"/api/v2/seasons/{community.Id}/members/{identity.Character.Id}/capabilities", new MemberCapabilities(["members.gameplay.manage", "groups.manage"]), etag: current.Etag))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantHome(owner, community, identity.Character.Id, "groups.manage");
        var edited = await Command<GroupProfile>(member, HttpMethod.Patch, root + "/" + created.Id, body with { Description = "Night patrol" }, created.Etag);
        using (var request = Request(HttpMethod.Post, root, body))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var request = Request(HttpMethod.Patch, root + "/" + created.Id, body, etag: created.Etag))
        using (var stale = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await GrantHome(owner, community, identity.Character.Id);
        // Revoking community Create Groups never removes ownership of an existing group.
        using (var request = Request(HttpMethod.Post, root, body))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var visible = (await member.GetFromJsonAsync<GroupPage>(root, TestContext.Current.CancellationToken))!; Assert.Single(visible.Items);
        await Command<GroupProfile>(owner, HttpMethod.Delete, root + "/" + created.Id, etag: edited.Etag);
        Assert.Empty((await member.GetFromJsonAsync<GroupPage>(root, TestContext.Current.CancellationToken))!.Items);
    }

    [Fact]
    public async Task Group_profiles_and_logos_are_community_scoped_bounded_and_recover_with_same_operation_key()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "912"); var community = await Create(owner);
        var root = $"/api/v2/seasons/{community.Id}/groups"; var key = Guid.CreateVersion7(clock.GetUtcNow()); var body = new GroupWrite("Arena team", new string('x', 300));
        GroupProfile created;
        using (var request = Request(HttpMethod.Post, root, body, key))
        using (var response = await owner.SendAsync(request, ct)) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); created = (await response.Content.ReadFromJsonAsync<CommandResult<GroupProfile>>(ct))!.Result; }
        using (var request = Request(HttpMethod.Post, root, body, key))
        using (var response = await owner.SendAsync(request, ct)) { Assert.True(response.IsSuccessStatusCode); Assert.Equal("true", response.Headers.GetValues("Idempotency-Replayed").Single()); Assert.Equal(created.Id, (await response.Content.ReadFromJsonAsync<CommandResult<GroupProfile>>(ct))!.Result.Id); }
        foreach (var bad in new[] { body with { Description = new string('x', 301) }, body with { Name = new string('x', 81) }, body with { Name = "\u0001" } })
        {
            using var request = Request(HttpMethod.Patch, root + "/" + created.Id, bad, etag: created.Etag); using var denied = await owner.SendAsync(request, ct); Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
        }
        var logoPath = root + "/" + created.Id + "/logo";
        using var uploaded = await UploadCommunityImage(owner, logoPath, LogoBytes(1024, 512), $"\"group-logo:{created.Id:N}:{created.LogoVersion}\"");
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var withLogo = (await uploaded.Content.ReadFromJsonAsync<CommandResult<GroupProfile>>(ct))!.Result; Assert.NotNull(withLogo.LogoAssetId);
        using var image = await owner.GetAsync(logoPath, ct); Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        using var stranger = f.CreateClient(); await Login(stranger, "913");
        using (var denied = await stranger.GetAsync(logoPath, ct)) Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        using (var before = await owner.GetAsync($"/api/v2/seasons/{community.Id}/logo", ct)) Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        await Command<GroupProfile>(owner, HttpMethod.Delete, root + "/" + created.Id, etag: withLogo.Etag);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.False(await db.MediaAssets.AnyAsync(x => x.Id == withLogo.LogoAssetId, ct));
        Assert.True(await db.Groups.AnyAsync(x => x.Id == created.Id && x.Deleted, ct));
        var other = await Create(stranger);
        using (var wrongParent = await stranger.GetAsync($"/api/v2/seasons/{other.Id}/groups/{created.Id}", ct)) Assert.Equal(HttpStatusCode.NotFound, wrongParent.StatusCode);
        Assert.False(db.Database.HasPendingModelChanges());
    }





}
