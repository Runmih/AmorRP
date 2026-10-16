using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Announcements;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private async Task<Member> GrantHome(HttpClient owner, Group group, Guid member, params string[] actions)
    {
        var current = await Member(owner, group, member);
        return await Command<Member>(owner, HttpMethod.Put, $"/api/v1/groups/{group.Id}/members/{member}/capabilities", new MemberCapabilities(actions), current.Etag);
    }
    private static async Task<AnnouncementPage> Posts(HttpClient client, Group group) =>
        (await client.GetFromJsonAsync<AnnouncementPage>($"/api/v1/groups/{group.Id}/announcements", TestContext.Current.CancellationToken))!;
    private async Task<Receipt> PostAnnouncement(HttpClient client, Group group, string title)
    {
        using var request = Request(HttpMethod.Post, $"/api/v1/groups/{group.Id}/announcements", new AnnouncementCreate(title, "Message for " + title));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Receipt>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task General_moderation_and_gameplay_management_are_separate_and_cannot_grant_moderator_authority()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "780"); var g = await Create(owner);
        using var moderator = f.CreateClient(); var mod = (await Login(moderator, "781")).Character.Id; await Join(moderator, await Invite(owner, g));
        using var member = f.CreateClient(); var target = (await Login(member, "782")).Character.Id; await Join(member, await Invite(owner, g));
        await GrantHome(owner, g, mod, "group.moderate");
        var live = (await moderator.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!;
        live = await Command<Group>(moderator, HttpMethod.Patch, $"/api/v1/groups/{g.Id}", new GroupEdit(live.Name, "Updated profile"), $"\"group:{g.Id:N}:{live.Version}\"");
        Assert.Equal("Updated profile", live.Description);
        var currency = await Command<AmorRP.Contracts.Currency.Currency>(moderator, HttpMethod.Patch, $"/api/v1/groups/{g.Id}/currency", new CurrencyEdit("Crowns", live.Currency.Symbol), $"\"currency:{live.Currency.Id:N}:{live.Currency.Version}\"");
        Assert.Equal("Crowns", currency.Name);
        using (var rename = Request(HttpMethod.Patch, $"/api/v1/groups/{g.Id}", new GroupEdit("Forbidden rename", ""), etag: $"\"group:{g.Id:N}:{live.Version}\""))
        using (var denied = await moderator.SendAsync(rename, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var symbol = Request(HttpMethod.Patch, $"/api/v1/groups/{g.Id}/currency", new CurrencyEdit("Crowns", "X"), etag: $"\"currency:{currency.Id:N}:{currency.Version}\""))
        using (var denied = await moderator.SendAsync(symbol, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var row = await Member(owner, g, target); var path = $"/api/v1/groups/{g.Id}/members/{target}/capabilities";
        using (var grant = Request(HttpMethod.Put, path, new MemberCapabilities(["items.potion.create"]), etag: row.Etag))
        using (var denied = await moderator.SendAsync(grant, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        foreach (var deniedCommand in new[] {
            Request(HttpMethod.Put, $"/api/v1/groups/{g.Id}/policies", new PolicyEdit(10, 10), etag: "\"unused\""),
            Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/invitations", new InvitationCreate(1, 1)),
            Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(mod, "1", "Forbidden")) })
        {
            using (deniedCommand) using (var denied = await moderator.SendAsync(deniedCommand, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        await GrantHome(owner, g, mod, "members.gameplay.manage");
        row = await GrantHome(owner, g, target, "items.potion.define");
        var changed = await Command<Member>(moderator, HttpMethod.Put, path, new MemberCapabilities(["items.potion.define", "items.potion.create"], false), row.Etag);
        Assert.True(changed.TradingRestricted); Assert.Contains("items.potion.create", changed.Capabilities);
        foreach (var forbidden in new[] { new[] { "items.potion.create" }, new[] { "items.potion.define", "items.potion.create", "currency.manage" }, new[] { "items.potion.define", "items.potion.create", "members.gameplay.manage" } })
        {
            using var grant = Request(HttpMethod.Put, path, new MemberCapabilities(forbidden), etag: changed.Etag);
            using var denied = await moderator.SendAsync(grant, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        changed = await Command<Member>(moderator, HttpMethod.Put, path, new MemberCapabilities(["items.potion.define"], true), changed.Etag);
        Assert.False(changed.TradingRestricted); Assert.DoesNotContain("items.potion.create", changed.Capabilities);
        await GrantHome(owner, g, mod);
        using var revoked = Request(HttpMethod.Put, path, new MemberCapabilities(changed.Capabilities, false), etag: changed.Etag);
        using var forbiddenResponse = await moderator.SendAsync(revoked, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task Announcement_retention_is_five_even_with_concurrent_creators_and_receipts_keep_no_message_content()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "783"); var g = await Create(owner);
        using var mod = f.CreateClient(); var id = (await Login(mod, "784")).Character.Id; await Join(mod, await Invite(owner, g));
        using (var unauthorized = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/announcements", new AnnouncementCreate("No", "No")))
        using (var denied = await mod.SendAsync(unauthorized, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantHome(owner, g, id, "announcements.manage");
        for (var i = 0; i < 7; i++) await PostAnnouncement(owner, g, "Notice " + i);
        var page = await Posts(mod, g); Assert.Equal(5, page.Items.Length);
        Assert.Equal(new[] { "Notice 6", "Notice 5", "Notice 4", "Notice 3", "Notice 2" }, page.Items.Select(x => x.Title));
        var doomed = page.Items[^1].Id;
        await Task.WhenAll(PostAnnouncement(owner, g, "Owner concurrent"), PostAnnouncement(mod, g, "Moderator concurrent"));
        page = await Posts(owner, g); Assert.Equal(5, page.Items.Length);
        using var old = await owner.GetAsync($"/api/v1/groups/{g.Id}/announcements/{doomed}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var path = $"/api/v1/groups/{g.Id}/announcements"; var body = new AnnouncementCreate("Replay title", "DO NOT RETAIN IN RECEIPT");
        using var create = Request(HttpMethod.Post, path, body, key); using var first = await mod.SendAsync(create, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var receipt = (await first.Content.ReadFromJsonAsync<Receipt>(TestContext.Current.CancellationToken))!;
        using var retry = Request(HttpMethod.Post, path, body, key); using var recovered = await mod.SendAsync(retry, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, recovered.StatusCode); Assert.Equal(receipt.OperationId, (await recovered.Content.ReadFromJsonAsync<Receipt>(TestContext.Current.CancellationToken))!.OperationId);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(5, await db.Announcements.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(10, (await db.Groups.SingleAsync(TestContext.Current.CancellationToken)).AnnouncementSequence);
        var operation = await db.Operations.SingleAsync(x => x.Id == receipt.OperationId, TestContext.Current.CancellationToken);
        var response = scope.ServiceProvider.GetRequiredService<SecretVault>().Unprotect(operation.ProtectedResponse, "operation:" + operation.Id);
        Assert.DoesNotContain(body.Body, response); Assert.DoesNotContain(body.Title, response); Assert.Equal("Announcement posted.", operation.Summary);
    }

    [Fact]
    public async Task Announcement_reads_are_character_scoped_persistent_bounded_and_do_not_change_member_versions_or_audit()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "785"); var g = await Create(owner);
        using var reader = f.CreateClient(); var identity = await Login(reader, "786"); await Join(reader, await Invite(owner, g));
        await PostAnnouncement(owner, g, "First"); await PostAnnouncement(owner, g, "Second");
        Assert.All((await Posts(owner, g)).Items, x => Assert.False(x.Unread));
        var page = await Posts(reader, g); Assert.All(page.Items, x => Assert.True(x.Unread)); var post = page.Items[0];
        using (var full = await reader.GetAsync($"/api/v1/groups/{g.Id}/announcements/{post.Id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.True((await Posts(reader, g)).Items[0].Unread);
        var before = await Member(owner, g, identity.Character.Id); int count;
        await using (var s = f.Services.CreateAsyncScope()) count = await s.ServiceProvider.GetRequiredService<AmorDbContext>().Operations.CountAsync(TestContext.Current.CancellationToken);
        for (var i = 0; i < 3; i++)
        {
            using var req = Request(HttpMethod.Put, $"/api/v1/groups/{g.Id}/announcements/{post.Id}/read");
            using var res = await reader.SendAsync(req, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(1, (await res.Content.ReadFromJsonAsync<AnnouncementReadResult>(TestContext.Current.CancellationToken))!.UnreadCount);
        }
        Assert.Equal(before.Etag, (await Member(owner, g, identity.Character.Id)).Etag);
        await using (var s = f.Services.CreateAsyncScope()) Assert.Equal(count, await s.ServiceProvider.GetRequiredService<AmorDbContext>().Operations.CountAsync(TestContext.Current.CancellationToken));
        using (var restarted = Factory()) using (var client = restarted.CreateClient())
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", identity.AccessToken);
            Assert.False((await Posts(client, g)).Items[0].Unread); Assert.True((await Posts(client, g)).Items[1].Unread);
        }
        for (var i = 0; i < 8; i++)
        {
            await PostAnnouncement(owner, g, "More " + i); var next = (await Posts(reader, g)).Items[0];
            using var req = Request(HttpMethod.Put, $"/api/v1/groups/{g.Id}/announcements/{next.Id}/read");
            using var res = await reader.SendAsync(req, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.InRange((await db.Memberships.SingleAsync(x => x.CharacterId == identity.Character.Id, TestContext.Current.CancellationToken)).ReadAnnouncementIds.Length, 1, 5);
    }

    [Fact]
    public async Task Announcements_reject_invalid_content_cross_group_and_former_member_access()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "787"); var g = await Create(owner);
        using var reader = f.CreateClient(); var identity = await Login(reader, "788"); await Join(reader, await Invite(owner, g));
        foreach (var body in new[] { new AnnouncementCreate("", "Text"), new AnnouncementCreate(new string('a',121), "Text"), new AnnouncementCreate("Title", new string('a',5001)), new AnnouncementCreate("Hidden\u202e", "Text") })
        {
            using var request = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/announcements", body); using var denied = await owner.SendAsync(request, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
        }
        await PostAnnouncement(owner, g, "Visible"); var post = (await Posts(reader, g)).Items.Single();
        var another = await Create(owner, "Other group");
        using (var cross = await owner.GetAsync($"/api/v1/groups/{another.Id}/announcements/{post.Id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        var target = await Member(owner, g, identity.Character.Id);
        await Command<Member>(owner, HttpMethod.Delete, $"/api/v1/groups/{g.Id}/members/{identity.Character.Id}", new MemberRemoval("Left"), target.Etag);
        await PostAnnouncement(owner, g, "After removal");
        foreach (var path in new[] { $"/api/v1/groups/{g.Id}/announcements", $"/api/v1/groups/{g.Id}/announcements/{post.Id}" })
        { using var response = await reader.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
        using var marker = Request(HttpMethod.Put, $"/api/v1/groups/{g.Id}/announcements/{post.Id}/read");
        using var forbidden = await reader.SendAsync(marker, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        var unread = (await reader.GetFromJsonAsync<AnnouncementUnreadPage>("/api/v1/announcements/unread", TestContext.Current.CancellationToken))!;
        Assert.Empty(unread.Groups);
    }
}
