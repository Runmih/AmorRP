using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.History;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Theory]
    [InlineData("leave")]
    [InlineData("remove")]
    [InlineData("block")]
    public async Task Former_members_cannot_read_community_history_or_later_moderation_even_with_an_old_cursor(string departure)
    {
        using var factory = Factory(); await Migrate(factory);
        using var owner = factory.CreateClient(); await Login(owner, "700"); var community = await Create(owner);
        using var member = factory.CreateClient(); var identity = await Login(member, "701");
        var invitation = await Invite(owner, community); var joinKey = Guid.CreateVersion7(clock.GetUtcNow());
        using var join = Request(HttpMethod.Post, "/api/v2/communities/join", new JoinCommunity(invitation.InvitationCode, true), joinKey);
        using var joined = await member.SendAsync(join, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(identity.Character.Id, "10", "Before departure"));
        var history = (await member.GetFromJsonAsync<HistoryEntryPage>($"/api/v2/communities/{community.Id}/history", TestContext.Current.CancellationToken))!;
        var ownReceipt = history.Items.First(x => x.ActorCharacterId == identity.Character.Id).OperationId;
        var page = (await member.GetFromJsonAsync<HistoryEntryPage>($"/api/v2/communities/{community.Id}/history?limit=1", TestContext.Current.CancellationToken))!;
        Assert.NotNull(page.NextCursor);
        var target = await Member(owner, community, identity.Character.Id);
        var method = departure == "block" ? HttpMethod.Put : HttpMethod.Delete;
        var path = $"/api/v2/communities/{community.Id}/members/{identity.Character.Id}" + (departure == "block" ? "/block" : "");
        var actor = departure == "leave" ? member : owner;
        target = await Command<Member>(actor, method, path, new MemberRemoval("Membership ended"), target.Etag);
        using var ended = await member.GetAsync($"/api/v2/communities/{community.Id}/history", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, ended.StatusCode);

        const string privateReason = "Private moderation review recorded after departure";
        var laterPath = $"/api/v2/communities/{community.Id}/members/{identity.Character.Id}/block";
        using var later = Request(departure == "block" ? HttpMethod.Delete : HttpMethod.Put, laterPath, new MemberRemoval(privateReason), etag: target.Etag);
        using var recorded = await owner.SendAsync(later, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var operation = (await recorded.Content.ReadFromJsonAsync<CommandResult<Member>>(TestContext.Current.CancellationToken))!.OperationId;

        using var denied = await member.GetAsync($"/api/v2/communities/{community.Id}/history", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain(privateReason, await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var cursor = await member.GetAsync($"/api/v2/communities/{community.Id}/history?cursor={Uri.EscapeDataString(page.NextCursor!)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, cursor.StatusCode);
        using var audit = await member.GetAsync($"/api/v2/communities/{community.Id}/audit", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, audit.StatusCode);
        var own = await member.GetFromJsonAsync<Operation>($"/api/v2/operations/{ownReceipt}", TestContext.Current.CancellationToken);
        Assert.Equal(ownReceipt, own!.Id);
        var byKey = await member.GetFromJsonAsync<Operation>($"/api/v2/operation-keys/{joinKey}", TestContext.Current.CancellationToken);
        Assert.Equal(ownReceipt, byKey!.Id);
        using var moderationReceipt = await member.GetAsync($"/api/v2/operations/{operation}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, moderationReceipt.StatusCode);
        var ownerAudit = (await owner.GetFromJsonAsync<HistoryEntryPage>($"/api/v2/communities/{community.Id}/audit", TestContext.Current.CancellationToken))!;
        Assert.Contains(ownerAudit.Items, x => x.OperationId == operation && x.Reason == privateReason);
        if (departure == "block")
        {
            await Join(member, await Invite(owner, community));
            using var rejoined = await member.GetAsync($"/api/v2/communities/{community.Id}/history", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, rejoined.StatusCode);
        }
    }

    [Fact]
    public async Task Active_history_is_personal_and_deleted_community_history_is_unavailable_to_owner_and_member()
    {
        using var factory = Factory(); await Migrate(factory);
        using var owner = factory.CreateClient(); var ownerIdentity = await Login(owner, "702"); var community = await Create(owner);
        using var member = factory.CreateClient(); var identity = await Login(member, "703"); await Join(member, await Invite(owner, community));
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(identity.Character.Id, "10", "Member-visible receipt"));
        const string unrelatedReason = "Owner-only adjustment";
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(ownerIdentity.Character.Id, "20", unrelatedReason));
        var history = (await member.GetFromJsonAsync<HistoryEntryPage>($"/api/v2/communities/{community.Id}/history", TestContext.Current.CancellationToken))!;
        Assert.Contains(history.Items, x => x.Reason == "Member-visible receipt");
        Assert.DoesNotContain(history.Items, x => x.Reason == unrelatedReason);
        var ownReceipt = history.Items.First(x => x.ActorCharacterId == identity.Character.Id).OperationId;
        using var deniedAudit = await member.GetAsync($"/api/v2/communities/{community.Id}/audit", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAudit.StatusCode);
        using var current = await owner.GetAsync($"/api/v2/communities/{community.Id}", TestContext.Current.CancellationToken);
        using var deletion = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/deletion", new CommunityDeletion(community.Name), etag: current.Headers.ETag!.ToString());
        using var deleted = await owner.SendAsync(deletion, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        foreach (var client in new[] { owner, member })
        {
            using var deniedHistory = await client.GetAsync($"/api/v2/communities/{community.Id}/history", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, deniedHistory.StatusCode);
            using var audit = await client.GetAsync($"/api/v2/communities/{community.Id}/audit", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, audit.StatusCode);
        }
        var recovered = await member.GetFromJsonAsync<Operation>($"/api/v2/operations/{ownReceipt}", TestContext.Current.CancellationToken);
        Assert.Equal(ownReceipt, recovered!.Id);
    }
}
