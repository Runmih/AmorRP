using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Pre_upgrade_permission_receipt_replays_with_its_original_body_hash()
    {
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "774"); var community = await Create(owner);
        using var member = factory.CreateClient(); var character = (await Login(member, "775")).Character.Id; await Join(member, await Invite(owner, community));
        var selected = await Member(owner, community, character); var key = Guid.CreateVersion7(clock.GetUtcNow());
        var path = $"/api/v2/communities/{community.Id}/members/{character}/capabilities";
        var oldBody = new { Capabilities = new[] { "items.potion.create" } };
        using var save = Request(HttpMethod.Put, path, oldBody, key, selected.Etag);
        using var saved = await owner.SendAsync(save, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var receipt = (await saved.Content.ReadFromJsonAsync<CommandResult<Member>>(TestContext.Current.CancellationToken))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            // Receipt persisted by the previous server, before TradingAllowed existed.
            var oldHash = SecretVault.Hash("PUT " + path + "\n" + selected.Etag + "\n{\"capabilities\":[\"items.potion.create\"]}");
            await db.Operations.Where(x => x.Id == receipt.OperationId).ExecuteUpdateAsync(x => x.SetProperty(o => o.RequestHash, oldHash), TestContext.Current.CancellationToken);
        }
        using var retry = Request(HttpMethod.Put, path, oldBody, key, selected.Etag);
        using var replay = await owner.SendAsync(retry, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(receipt.OperationId, (await replay.Content.ReadFromJsonAsync<CommandResult<Member>>(TestContext.Current.CancellationToken))!.OperationId);
        using var changed = Request(HttpMethod.Put, path, new MemberCapabilities(["items.potion.create"], false), key, selected.Etag);
        using var rejected = await owner.SendAsync(changed, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("idempotency_mismatch", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False((await Member(owner, community, character)).TradingRestricted);
    }

    [Fact]
    public async Task Recipe_permission_is_independent_of_crafting_and_revocation_blocks_cached_recipe_commands()
    {
        using var factory = Factory(); await Migrate(factory);
        using var owner = factory.CreateClient(); await Login(owner, "770"); var community = await Create(owner);
        using var member = factory.CreateClient(); var character = (await Login(member, "771")).Character.Id;
        await Join(member, await Invite(owner, community)); await GrantPotions(owner, community, character);
        var category = (await owner.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{community.Id}/categories", TestContext.Current.CancellationToken))!.Items.Single(x => x.AllowedTypeIds.Contains("potion"));
        var body = new DefinitionWrite("potion", "Moderator potion", category.Id, "A recipe", true, new(1, "drinks a potion."));
        var path = $"/api/v2/communities/{community.Id}/item-definitions";
        using (var request = Request(HttpMethod.Post, path, body))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var current = await Member(owner, community, character);
        await Command<Member>(owner, HttpMethod.Put, $"/api/v2/communities/{community.Id}/members/{character}/capabilities", new MemberCapabilities(["items.potion.define"]), current.Etag);
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        using var create = Request(HttpMethod.Post, path, body, key);
        using var created = await member.SendAsync(create, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var recipe = (await created.Content.ReadFromJsonAsync<CommandResult<Definition>>(TestContext.Current.CancellationToken))!.Result;
        using (var craft = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, 1)))
        using (var denied = await member.SendAsync(craft, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var categoryCreate = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/categories", new CategoryEdit("Forbidden", ["potion"])))
        using (var denied = await member.SendAsync(categoryCreate, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        current = await Member(owner, community, character);
        using (var escalate = Request(HttpMethod.Put, $"/api/v2/communities/{community.Id}/members/{character}/capabilities", new MemberCapabilities(["currency.manage"]), etag: current.Etag))
        using (var denied = await member.SendAsync(escalate, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        recipe = await Command<Definition>(member, HttpMethod.Patch, path + "/" + recipe.Id, body with { Name = "Revised recipe" }, $"\"definition:{recipe.Id:N}:{recipe.Revision}\"");
        current = await Member(owner, community, character);
        await Command<Member>(owner, HttpMethod.Put, $"/api/v2/communities/{community.Id}/members/{character}/capabilities", new MemberCapabilities([]), current.Etag);
        foreach (var action in new[] { "replay", "edit", "retire" })
        {
            using var request = action switch
            {
                "replay" => Request(HttpMethod.Post, path, body, key),
                "edit" => Request(HttpMethod.Patch, path + "/" + recipe.Id, body, etag: $"\"definition:{recipe.Id:N}:{recipe.Revision}\""),
                _ => Request(HttpMethod.Post, path + "/" + recipe.Id + "/retirement", etag: $"\"definition:{recipe.Id:N}:{recipe.Revision}\"")
            };
            using var denied = await member.SendAsync(request, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        current = await Member(owner, community, character);
        await Command<Member>(owner, HttpMethod.Put, $"/api/v2/communities/{community.Id}/members/{character}/capabilities", new MemberCapabilities(["items.potion.define"]), current.Etag);
        var retired = await Command<Definition>(member, HttpMethod.Post, path + "/" + recipe.Id + "/retirement", etag: $"\"definition:{recipe.Id:N}:{recipe.Revision}\"");
        Assert.True(retired.Retired);
    }

    [Fact]
    public async Task Gameplay_permission_save_is_atomic_defaults_to_trading_and_releases_reservations_without_changing_ownership_proposals()
    {
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); var actor = await Login(owner, "772"); var community = await Create(owner);
        using var member = factory.CreateClient(); var character = (await Login(member, "773")).Character.Id;
        await Join(member, await Invite(owner, community)); var current = await Member(owner, community, character);
        Assert.False(current.TradingRestricted); Assert.Empty(current.Capabilities);
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(actor.Character.Id, "10", "Seed"));
        var trade = await Command<Trade>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/trades", new TradeCreate(character));
        trade = await TradeCommand(member, community, trade, "accept"); trade = await Offer(owner, community, trade, "4");
        var proposal = await Command<OwnershipTransfer>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/ownership-transfers", new OwnershipProposal(character));
        var path = $"/api/v2/communities/{community.Id}/members/{character}/capabilities";
        var invalid = new MemberCapabilities(["unknown.action"], false);
        using (var request = Request(HttpMethod.Put, path, invalid, etag: current.Etag))
        using (var denied = await owner.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
        Assert.False((await Member(owner, community, character)).TradingRestricted); Assert.Equal("open", (await ReadTrade(owner, community, trade)).State);
        var body = new MemberCapabilities(["items.potion.create", "items.potion.define"], false);
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        using var save = Request(HttpMethod.Put, path, body, key, current.Etag);
        using var response = await owner.SendAsync(save, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CommandResult<Member>>(TestContext.Current.CancellationToken))!;
        Assert.True(result.Result.TradingRestricted); Assert.Contains("items.potion.define", result.Result.Capabilities);
        Assert.Equal("cancelled", (await ReadTrade(owner, community, trade)).State);
        Assert.Equal("0", (await owner.GetFromJsonAsync<BalancePage>($"/api/v2/communities/{community.Id}/members/{actor.Character.Id}/balances", TestContext.Current.CancellationToken))!.Items.Single().Reserved);
        var unchanged = (await owner.GetFromJsonAsync<OwnershipTransfer>($"/api/v2/communities/{community.Id}/ownership-transfers/{proposal.Id}", TestContext.Current.CancellationToken))!;
        Assert.Equal("proposed", unchanged.Status);
        using var retry = Request(HttpMethod.Put, path, body, key, current.Etag);
        using var recovered = await owner.SendAsync(retry, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(result.OperationId, (await recovered.Content.ReadFromJsonAsync<CommandResult<Member>>(TestContext.Current.CancellationToken))!.OperationId);
        using var stale = Request(HttpMethod.Put, path, new MemberCapabilities([], true), etag: current.Etag);
        using var conflict = await owner.SendAsync(stale, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.PreconditionFailed, conflict.StatusCode);
        var legacy = await Command<Member>(owner, HttpMethod.Put, path, new { Capabilities = new[] { "items.potion.define" } }, result.Result.Etag);
        Assert.True(legacy.TradingRestricted);
        var allowed = await Command<Member>(owner, HttpMethod.Put, path, new MemberCapabilities(legacy.Capabilities, true), legacy.Etag);
        Assert.False(allowed.TradingRestricted); Assert.Contains("items.potion.define", allowed.Capabilities);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(3, await db.Operations.CountAsync(x => x.CommunityId == community.Id && x.Kind == "member.capabilities", TestContext.Current.CancellationToken));
        Assert.Null((await db.Operations.SingleAsync(x => x.Id == result.OperationId, TestContext.Current.CancellationToken)).Reason);
    }
}
