using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private async Task GroupAction(HttpClient client, HttpMethod method, string path, object? body = null, string? etag = null)
    {
        using var request = Request(method, path, body, etag: etag);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Group_owner_controls_contents_and_community_owner_has_deletion_only_without_private_access()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f);
        using var owner = f.CreateClient(); await Login(owner, "1001"); var c = await Create(owner);
        using var creator = f.CreateClient(); var creatorId = (await Login(creator, "1002")).Character.Id; await Join(creator, await Invite(owner, c));
        using var peer = f.CreateClient(); var peerId = (await Login(peer, "1003")).Character.Id; await Join(peer, await Invite(owner, c));
        await GrantHome(owner, c, creatorId, "groups.create");
        var root = $"/api/v2/communities/{c.Id}/groups";
        var g = await Command<GroupProfile>(creator, HttpMethod.Post, root, new GroupWrite("Guard", "Patrol", "password", "secret-password"));
        var path = root + "/" + g.Id;
        await GrantHome(owner, c, peerId, "groups.manage");
        Assert.Equal(creatorId, g.OwnerCharacterId); Assert.True(g.IsMember);
        foreach (var client in new[] { owner, peer })
        {
            using var denied = await client.GetAsync(path + "/private", ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var request = Request(HttpMethod.Patch, path, new GroupWrite("Override", ""), etag: g.Etag);
            using var edit = await client.SendAsync(request, ct); Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        }
        using (var request = Request(HttpMethod.Post, path + "/announcements", new AnnouncementWrite("Override", "Denied")))
        using (var denied = await owner.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GroupAction(creator, HttpMethod.Post, path + "/announcements", new AnnouncementWrite("Private patrol", "MEMBERS ONLY"));
        var own = (await creator.GetFromJsonAsync<GroupPrivate>(path + "/private", ct))!;
        Assert.Single(own.Members); Assert.Equal("MEMBERS ONLY", Assert.Single(own.Announcements).Message);
        using (var request = Request(HttpMethod.Post, path + "/join", new GroupJoin("wrong")))
        using (var denied = await peer.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GroupAction(peer, HttpMethod.Post, path + "/join", new GroupJoin("secret-password"));
        Assert.Equal(2, (await peer.GetFromJsonAsync<GroupPrivate>(path + "/private", ct))!.Members.Length);
        var firstPage = (await peer.GetFromJsonAsync<GroupPrivate>(path + "/private?limit=1", ct))!;
        Assert.Single(firstPage.Members); Assert.NotNull(firstPage.NextCursor);
        var secondPage = (await peer.GetFromJsonAsync<GroupPrivate>(path + "/private?limit=1&cursor=" + Uri.EscapeDataString(firstPage.NextCursor!), ct))!;
        Assert.Single(secondPage.Members); Assert.Null(secondPage.NextCursor);
        Assert.NotEqual(firstPage.Members[0].CharacterId, secondPage.Members[0].CharacterId);
        for (var i = 0; i < 5; i++) await GroupAction(creator, HttpMethod.Post, path + "/announcements", new AnnouncementWrite("Update " + i, "Private message " + i));
        Assert.Equal(5, (await peer.GetFromJsonAsync<GroupPrivate>(path + "/private", ct))!.Announcements.Length);

        await GroupAction(creator, HttpMethod.Put, path + "/notice", new NoticeWrite("Help wanted", "Escort requested", "10 coins"));
        var hub = (await owner.GetFromJsonAsync<GroupHub>($"/api/v2/communities/{c.Id}/group-hub", ct))!;
        Assert.Equal("10 coins", Assert.Single(hub.Notices).Reward);
        await GroupAction(creator, HttpMethod.Put, path + "/notice", new NoticeWrite("Updated", "New request", "20 coins"), $"\"group-notice:{g.Id:N}:1\"");
        hub = (await peer.GetFromJsonAsync<GroupHub>($"/api/v2/communities/{c.Id}/group-hub", ct))!;
        Assert.Equal("Updated", Assert.Single(hub.Notices).Title);
        var start = clock.GetUtcNow().AddDays(1);
        await GroupAction(creator, HttpMethod.Post, path + "/events", new EventWrite(new string('x', 40), new string('y', 100), "Limsa", start, start.AddHours(2)));
        await GroupAction(creator, HttpMethod.Post, path + "/events", new EventWrite("Second", "Another patrol", "Gridania", start.AddDays(1), start.AddDays(1).AddHours(1)));
        using (var request = Request(HttpMethod.Post, path + "/events", new EventWrite(new string('x', 41), "", "Limsa", start, start.AddHours(1))))
        using (var denied = await creator.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
        await GroupAction(peer, HttpMethod.Put, path + "/favorite");
        Assert.Equal(2, (await peer.GetFromJsonAsync<FavoriteEvents>("/api/v2/me/favorite-group-events", ct))!.Items.Length);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            var stored = await db.Groups.SingleAsync(ct); Assert.NotEqual("secret-password", stored.PasswordHash); Assert.NotNull(stored.PasswordHash);
            Assert.Equal(0, (await db.Memberships.SingleAsync(x => x.CharacterId == peerId, ct)).Owned);
            Assert.Single(await db.GroupNotices.ToArrayAsync(ct)); Assert.False(db.Database.HasPendingModelChanges());
        }
        await Command<GroupProfile>(owner, HttpMethod.Delete, path, etag: g.Etag);
        Assert.Empty((await peer.GetFromJsonAsync<FavoriteEvents>("/api/v2/me/favorite-group-events", ct))!.Items);
        using var gone = await peer.GetAsync(path + "/private", ct); Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
    [Fact]
    public async Task Three_owned_groups_are_enforced_across_communities_and_joining_more_than_three_is_allowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f);
        using var owner = f.CreateClient(); await Login(owner, "1010"); var c = await Create(owner); var other = await Create(owner, "Other");
        using var member = f.CreateClient(); var id = (await Login(member, "1011")).Character.Id;
        await Join(member, await Invite(owner, c)); await Join(member, await Invite(owner, other));
        await GrantHome(owner, c, id, "groups.create"); await GrantHome(owner, other, id, "groups.create");
        var a = await Command<GroupProfile>(member, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("One", ""));
        await Command<GroupProfile>(member, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Two", ""));
        await Command<GroupProfile>(member, HttpMethod.Post, $"/api/v2/communities/{other.Id}/groups", new GroupWrite("Three", ""));
        using (var request = Request(HttpMethod.Post, $"/api/v2/communities/{other.Id}/groups", new GroupWrite("Fourth", "")))
        using (var denied = await member.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        for (var i = 0; i < 3; i++)
        {
            var g = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Public " + i, ""));
            await GroupAction(member, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups/{g.Id}/join", new GroupJoin());
        }
        await using (var scope = f.Services.CreateAsyncScope())
            Assert.Equal(6, await scope.ServiceProvider.GetRequiredService<AmorDbContext>().GroupMemberships.CountAsync(x => x.CharacterId == id, ct));
        await Command<GroupProfile>(member, HttpMethod.Delete, $"/api/v2/communities/{c.Id}/groups/{a.Id}", etag: a.Etag);
        await Command<GroupProfile>(member, HttpMethod.Post, $"/api/v2/communities/{other.Id}/groups", new GroupWrite("Replacement", ""));
        var closed = await Command<GroupProfile>(owner, HttpMethod.Patch, $"/api/v2/communities/{c.Id}/groups/" + (await owner.GetFromJsonAsync<GroupPage>($"/api/v2/communities/{c.Id}/groups", ct))!.Items.First(x => x.OwnerCharacterId != id).Id,
            new GroupWrite("Closed", "", "closed"), (await owner.GetFromJsonAsync<GroupPage>($"/api/v2/communities/{c.Id}/groups", ct))!.Items.First(x => x.OwnerCharacterId != id).Etag);
        using var outsider = f.CreateClient(); await Login(outsider, "1012"); await Join(outsider, await Invite(owner, c));
        using var join = Request(HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups/{closed.Id}/join", new GroupJoin());
        using var rejected = await outsider.SendAsync(join, ct); Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }
    [Fact]
    public async Task Password_guess_limit_does_not_exhaust_XIVAuth_login_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory().WithWebHostBuilder(b => b.UseSetting("ServicePolicy:LoginRequestsPerMinute", "60"));
        await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1060"); var c = await Create(owner);
        using var member = f.CreateClient(); await Login(member, "1061"); await Join(member, await Invite(owner, c));
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Protected", "", "password", "correct-password"));
        var limited = false;
        for (var n = 0; n < 50; n++)
        {
            using var attempt = Request(HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups/{group.Id}/join", new GroupJoin("incorrect-password"));
            using var response = await member.SendAsync(attempt, ct);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests });
            limited |= response.StatusCode == HttpStatusCode.TooManyRequests;
        }
        Assert.True(limited);
        using var login = Request(HttpMethod.Post, "/api/v2/auth/login-attempts", new AmorRP.Contracts.Auth.LoginStart("Person 1062", "35", "Zalera"));
        login.Headers.Add("X-Login-Request-Credential", AmorRP.Server.Infrastructure.Security.SecretVault.NewSecret());
        using var created = await member.SendAsync(login, ct); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

}
