using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Inventory;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task M4_upgrade_preserves_every_existing_category_policy_including_seed_like_names()
    {
        using var factory = Factory(); await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261007165023_PlayableInventory", ct);
        var actor = Guid.NewGuid(); var community = Guid.NewGuid(); var currency = Guid.NewGuid(); var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt") VALUES ({actor}, '740', 'binding', 'M3 owner', '35', 'Zalera', {now});
            INSERT INTO groups ("Id", "Name", "Description", "OwnerCharacterId", "Version", "Deleted", "CurrencyId", "CurrencyName", "CurrencySymbol", "CurrencyVersion", "PolicyVersion", "CurrentPotionPoints", "CurrentLetters", "NextPotionPoints", "NextLetters", "PolicyPeriod", "InventoryVersion") VALUES ({community}, 'M3 categories', '', {actor}, 1, false, {currency}, 'Coins', 'C', 1, 1, 12, 5, 12, 5, {now}, 4);
            """, ct);
        var categories = new[]
        {
            new CategoryRow { CommunityId = community, Name = "Owner letters", AllowedTypeIds = ["letter"], Version = 4 },
            new CategoryRow { CommunityId = community, Name = "Bottled letters", AllowedTypeIds = ["potion", "letter"], Version = 2 },
            new CategoryRow { CommunityId = community, Name = "Correspondence", AllowedTypeIds = ["letter"] },
            new CategoryRow { CommunityId = community, Name = "Books", AllowedTypeIds = ["letter"], Version = 8 },
            new CategoryRow { CommunityId = community, Name = "Archived letters", AllowedTypeIds = ["letter"], Retired = true, Version = 5 },
            new CategoryRow { CommunityId = community, Name = "Herbalism", AllowedTypeIds = ["potion"], Version = 3 }
        };
        foreach (var category in categories) await LegacyInsert(db, category, ct); db.ChangeTracker.Clear();
        await db.Database.MigrateAsync(ct);
        var upgraded = await db.Categories.ToDictionaryAsync(x => x.Id, ct);
        Assert.Equal(categories.Length, upgraded.Count);
        foreach (var before in categories)
        {
            var after = upgraded[before.Id];
            Assert.Equal(before.Name, after.Name); Assert.Equal(before.AllowedTypeIds, after.AllowedTypeIds);
            Assert.Equal(before.Version, after.Version); Assert.Equal(before.Retired, after.Retired);
        }
        Assert.Equal(4, (await db.Communities.SingleAsync(ct)).InventoryVersion);
    }

    [Fact]
    public async Task Already_broadened_policies_need_owner_edits_and_narrowing_preserves_existing_written_items()
    {
        using var factory = Factory(); await Migrate(factory);
        using var owner = factory.CreateClient(); await Login(owner, "741"); var community = await Create(owner);
        var defaults = (await owner.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{community.Id}/categories", TestContext.Current.CancellationToken))!.Items;
        Assert.Equal(new[] { "book", "letter", "note" }, defaults.Single(x => x.Name == "Books").AllowedTypeIds);
        var category = await Command<Category>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/categories", new CategoryEdit("Owner letters", ["letter"]));
        // Simulate the old applied migration. A newer binary does not re-run a
        // migration already in history, nor guess which later policies to undo.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE categories SET \"AllowedTypeIds\" = ARRAY['letter','book','note']::text[], \"Version\" = \"Version\" + 1 WHERE \"CommunityId\" = {community.Id} AND \"Id\" = {category.Id};", TestContext.Current.CancellationToken);
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }
        var existing = await Command<ProductionResult>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/inventory/letters", new LetterCreate(category.Id, "Existing book", "Retained body", "book"));
        using var current = await owner.GetAsync($"/api/v2/communities/{community.Id}/categories/{category.Id}", TestContext.Current.CancellationToken);
        using var member = factory.CreateClient(); await Login(member, "742"); await Join(member, await Invite(owner, community));
        using var forbidden = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}/categories/{category.Id}", new CategoryEdit(category.Name, ["letter"]), etag: current.Headers.ETag!.ToString());
        using var denied = await member.SendAsync(forbidden, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var repaired = await Command<Category>(owner, HttpMethod.Patch, $"/api/v2/communities/{community.Id}/categories/{category.Id}", new CategoryEdit(category.Name, ["letter"]), current.Headers.ETag!.ToString());
        Assert.Equal(category.Id, repaired.Id); Assert.Equal(category.Name, repaired.Name); Assert.Equal(new[] { "letter" }, repaired.AllowedTypeIds);
        Assert.Equal(category.Version + 2, repaired.Version);
        foreach (var type in new[] { "book", "note" })
        {
            using var create = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/inventory/letters", new LetterCreate(category.Id, "Blocked new type", "", type));
            using var rejected = await owner.SendAsync(create, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        }
        var written = (await owner.GetFromJsonAsync<Letter>($"/api/v2/communities/{community.Id}/letters/{existing.Holding.LetterId}", TestContext.Current.CancellationToken))!;
        Assert.Equal("book", written.TypeId); Assert.Equal("Retained body", written.Body);
        await Command<ProductionResult>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/inventory/letters", new LetterCreate(category.Id, "Allowed letter", "", "letter"));
    }
}
