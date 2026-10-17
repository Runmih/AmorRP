using System.Net.Http.Json;
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
    [Fact]
    public async Task Upgrade_preserves_group_profiles_assigns_legacy_ownership_and_keeps_custom_categories_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var actor = (await Login(owner, "1040")).Character.Id; var c = await Create(owner);
        var g = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Existing profile", "Retain this"));
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var categories = await db.Categories.AsNoTracking().ToArrayAsync(ct);
        await db.GetService<IMigrator>().MigrateAsync("20261017002828_CommunitiesAndGroups", ct);
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync(ct);
        var restored = await db.Groups.SingleAsync(ct);
        Assert.Equal(g.Id, restored.Id); Assert.Equal(g.Name, restored.Name); Assert.Equal(g.Description, restored.Description);
        Assert.Equal(actor, restored.OwnerCharacterId); Assert.Equal("open", restored.JoinMode);
        var member = await db.GroupMemberships.SingleAsync(ct); Assert.Equal(actor, member.CharacterId); Assert.Equal(g.Id, member.GroupId);
        foreach (var before in categories)
        {
            var after = await db.Categories.SingleAsync(x => x.Id == before.Id, ct);
            Assert.Equal(before.AllowedTypeIds, after.AllowedTypeIds); Assert.Equal(before.Version, after.Version);
        }
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task Concurrent_group_creations_cannot_exceed_three_owned_groups()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1041"); var c = await Create(owner);
        var tasks = Enumerable.Range(0, 4).Select(async i => {
            using var request = Request(HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Group " + i, ""));
            using var response = await owner.SendAsync(request, ct); return response.StatusCode;
        });
        var results = await Task.WhenAll(tasks);
        Assert.Equal(3, results.Count(x => x == System.Net.HttpStatusCode.Created));
        Assert.Equal(1, results.Count(x => x == System.Net.HttpStatusCode.Conflict));
    }
}
