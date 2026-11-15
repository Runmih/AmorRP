using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Typed_group_handlers_preserve_updates_deletions_membership_and_receipt_replay()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory);
        using var owner = factory.CreateClient(); await Login(owner, "1010"); var community = await Create(owner);
        using var peer = factory.CreateClient(); await Login(peer, "1011"); await Enroll(peer, community);
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/seasons/{community.Id}/groups", new GroupWrite("Patrol", ""));
        var path = $"/api/v2/seasons/{community.Id}/groups/{group.Id}";
        await GroupAction(peer, HttpMethod.Post, path + "/join", new GroupJoin());
        await GroupAction(peer, HttpMethod.Put, path + "/favorite");
        var start = clock.GetUtcNow().AddDays(1);
        await GroupAction(owner, HttpMethod.Post, path + "/events", new EventWrite("Patrol", "Patrol details", "Limsa", start, start.AddHours(1)));
        var hubPath = $"/api/v2/seasons/{community.Id}/group-hub";
        var item = Assert.Single((await owner.GetFromJsonAsync<GroupHub>(hubPath, ct))!.Events);
        await GroupAction(owner, HttpMethod.Put, path + "/events/" + item.Id,
            new EventWrite("Updated patrol", "Patrol details", "Gridania", start, start.AddHours(2)), $"\"group-event:{item.Id:N}:1\"");
        Assert.Equal("Updated patrol", Assert.Single((await peer.GetFromJsonAsync<FavoriteEvents>("/api/v2/me/favorite-group-events", ct))!.Items).Title);
        await GroupAction(peer, HttpMethod.Delete, path + "/favorite");
        Assert.Empty((await peer.GetFromJsonAsync<FavoriteEvents>("/api/v2/me/favorite-group-events", ct))!.Items);
        await GroupAction(owner, HttpMethod.Put, path + "/notice", new NoticeWrite("Help", "Wanted", "Coins"));
        await GroupAction(owner, HttpMethod.Delete, path + "/notice", etag: $"\"group-notice:{group.Id:N}:1\"");
        await GroupAction(owner, HttpMethod.Delete, path + "/events/" + item.Id, etag: $"\"group-event:{item.Id:N}:2\"");
        var hub = (await owner.GetFromJsonAsync<GroupHub>(hubPath, ct))!;
        Assert.Empty(hub.Notices); Assert.Empty(hub.Events);
        var key = Guid.CreateVersion7();
        for (var i = 0; i < 2; i++)
        {
            using var request = Request(HttpMethod.Post, path + "/announcements", new AnnouncementWrite("Orders", "Private"), key);
            using var reply = await owner.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
            Assert.Equal(i == 1, reply.Headers.Contains("Idempotency-Replayed"));
        }
        Assert.Single((await peer.GetFromJsonAsync<GroupPrivate>(path + "/private", ct))!.Announcements);
        await GroupAction(peer, HttpMethod.Delete, path + "/membership");
        using var privateDenied = await peer.GetAsync(path + "/private", ct);
        Assert.Equal(HttpStatusCode.Forbidden, privateDenied.StatusCode);
    }

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
        using var creator = f.CreateClient(); var creatorId = (await Login(creator, "1002")).Character.Id; await Enroll(creator, c);
        using var peer = f.CreateClient(); var peerId = (await Login(peer, "1003")).Character.Id; await Enroll(peer, c);
        await GrantHome(owner, c, creatorId, "groups.create");
        var root = $"/api/v2/seasons/{c.Id}/groups";
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
        var hub = (await owner.GetFromJsonAsync<GroupHub>($"/api/v2/seasons/{c.Id}/group-hub", ct))!;
        Assert.Equal("10 coins", Assert.Single(hub.Notices).Reward);
        await GroupAction(creator, HttpMethod.Put, path + "/notice", new NoticeWrite("Updated", "New request", "20 coins"), $"\"group-notice:{g.Id:N}:1\"");
        hub = (await peer.GetFromJsonAsync<GroupHub>($"/api/v2/seasons/{c.Id}/group-hub", ct))!;
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
    public async Task Group_ownership_limit_is_per_season_and_joining_more_than_three_is_allowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f);
        using var owner = f.CreateClient(); await Login(owner, "1010"); var c = await Create(owner);
        using var member = f.CreateClient(); var id = (await Login(member, "1011")).Character.Id;
        await GrantHome(owner, c, id, "groups.create");
        for (var i = 0; i < 3; i++)
        {
            await Command<GroupProfile>(member, HttpMethod.Post, $"/api/v2/seasons/{c.Id}/groups", new GroupWrite("Owned " + i, ""));
            var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/seasons/{c.Id}/groups", new GroupWrite("Public " + i, ""));
            await GroupAction(member, HttpMethod.Post, $"/api/v2/seasons/{c.Id}/groups/{group.Id}/join", new GroupJoin());
        }
        using (var fourth = Request(HttpMethod.Post, $"/api/v2/seasons/{c.Id}/groups", new GroupWrite("Fourth", "")))
            Assert.Equal(HttpStatusCode.Conflict, (await member.SendAsync(fourth, ct)).StatusCode);
        await using (var scope = f.Services.CreateAsyncScope())
            Assert.Equal(6, await scope.ServiceProvider.GetRequiredService<AmorDbContext>().GroupMemberships.CountAsync(x => x.CommunityId == c.Id && x.CharacterId == id, ct));
        var next = await Create(owner, "Next season");
        await GrantHome(owner, next, id, "groups.create");
        await Command<GroupProfile>(member, HttpMethod.Post, $"/api/v2/seasons/{next.Id}/groups", new GroupWrite("Fresh quota", ""));
        Assert.Equal("archived", (await owner.GetFromJsonAsync<Season>($"/api/v2/seasons/{c.Id}", ct))!.SeasonState);
    }
    [Fact]
    public async Task Password_guess_limit_does_not_exhaust_XIVAuth_login_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory().WithWebHostBuilder(b => b.UseSetting("ServicePolicy:LoginRequestsPerMinute", "60"));
        await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1060"); var c = await Create(owner);
        using var member = f.CreateClient(); await Login(member, "1061"); await Enroll(member, c);
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/seasons/{c.Id}/groups", new GroupWrite("Protected", "", "password", "correct-password"));
        var limited = false;
        for (var n = 0; n < 50; n++)
        {
            using var attempt = Request(HttpMethod.Post, $"/api/v2/seasons/{c.Id}/groups/{group.Id}/join", new GroupJoin("incorrect-password"));
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
