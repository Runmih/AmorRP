using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private static async Task<Member> ReadProfile(HttpClient client, Community community, Guid characterId) =>
        (await client.GetFromJsonAsync<Member>($"/api/v2/communities/{community.Id}/members/{characterId}", TestContext.Current.CancellationToken))!;

    [Fact]
    public async Task Descriptions_are_self_edited_community_scoped_and_visible_only_through_authorized_membership()
    {
        using var factory = Factory(); await Migrate(factory);
        using var owner = factory.CreateClient(); var ownerIdentity = await Login(owner, "760"); var community = await Create(owner);
        using var member = factory.CreateClient(); var identity = await Login(member, "761"); await Join(member, await Invite(owner, community));
        var current = await ReadProfile(member, community, identity.Character.Id); Assert.Equal("", current.Description);
        var saved = await Command<Member>(member, HttpMethod.Patch, $"/api/v2/communities/{community.Id}/members/{identity.Character.Id}/description",
            new MemberDescriptionEdit("A wandering healer.\nLikes tea <3"), current.Etag);
        Assert.Equal("A wandering healer.\nLikes tea <3", (await ReadProfile(owner, community, identity.Character.Id)).Description);
        Assert.Equal(current.Version + 1, saved.Version);
        foreach (var attempt in new[] { (owner, identity.Character.Id, saved.Etag), (member, ownerIdentity.Character.Id, (await ReadProfile(member, community, ownerIdentity.Character.Id)).Etag) })
        {
            using var request = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}/members/{attempt.Item2}/description", new MemberDescriptionEdit("Overwrite"), etag: attempt.Item3);
            using var rejected = await attempt.Item1.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        }
        var otherCommunity = await Create(member, "Different RP setting");
        Assert.Equal("", (await ReadProfile(member, otherCommunity, identity.Character.Id)).Description);
        using var stranger = factory.CreateClient(); await Login(stranger, "762");
        using var hidden = await stranger.GetAsync($"/api/v2/communities/{community.Id}/members/{identity.Character.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        await Command<Member>(member, HttpMethod.Delete, $"/api/v2/communities/{community.Id}/members/{identity.Character.Id}", new MemberRemoval(""), saved.Etag);
        using var stale = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}/members/{identity.Character.Id}/description", new MemberDescriptionEdit("After leaving"), etag: saved.Etag);
        using var inactive = await member.SendAsync(stale, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
        await Join(member, await Invite(owner, community));
        Assert.Equal(saved.Description, (await ReadProfile(member, community, identity.Character.Id)).Description);
    }

    [Fact]
    public async Task Profile_save_checks_length_plain_text_concurrency_and_same_key_recovery()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient(); var identity = await Login(client, "763"); var community = await Create(client);
        var path = $"/api/v2/communities/{community.Id}/members/{identity.Character.Id}/description";
        var current = await ReadProfile(client, community, identity.Character.Id);
        var text = string.Concat(Enumerable.Repeat("🌸", 300));
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var body = new MemberDescriptionEdit(text);
        using var request = Request(HttpMethod.Patch, path, body, key, current.Etag);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<CommandResult<Member>>(TestContext.Current.CancellationToken))!;
        Assert.Equal(text, receipt.Result.Description);
        using var retry = Request(HttpMethod.Patch, path, body, key, current.Etag);
        using var recovered = await client.SendAsync(retry, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(receipt.OperationId, (await recovered.Content.ReadFromJsonAsync<CommandResult<Member>>(TestContext.Current.CancellationToken))!.OperationId);
        using var stale = Request(HttpMethod.Patch, path, new MemberDescriptionEdit("Conflicting edit"), etag: current.Etag);
        using var conflict = await client.SendAsync(stale, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionFailed, conflict.StatusCode);
        foreach (var invalid in new string?[] { new('a', 301), "Hidden\u202etext", "Control\u0001", null })
        {
            using var bad = Request(HttpMethod.Patch, path, new { Description = invalid }, etag: receipt.Result.Etag);
            using var rejected = await client.SendAsync(bad, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        }
        var cleared = await Command<Member>(client, HttpMethod.Patch, path, new MemberDescriptionEdit(""), receipt.Result.Etag);
        Assert.Equal("", cleared.Description);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var operations = await db.Operations.Where(x => x.Kind == "member.description").ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, operations.Length); Assert.All(operations, op => Assert.Equal("Character description updated.", op.Summary));
    }
}
