using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Theory]
    [InlineData("dormant")]
    [InlineData("blocked")]
    public async Task Shared_membership_migration_restores_all_seasons_without_resetting_balances_or_reviving_grants(string previousStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = SeasonFactory(); await Migrate(f);
        using var admin = f.CreateClient(); await Login(admin, "90001");
        using var player = f.CreateClient(); var identity = await Login(player, "90021");
        var season = await Draft(admin, "Existing"); await Transition(admin, season);
        var other = await Draft(admin, "Upcoming");
        await Command<Balance>(admin, HttpMethod.Post, $"/api/v2/seasons/{season.Id}/currency/adjustments", new CurrencyAdjustment(identity.Character.Id, "75", "Preserve existing funds"));
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            await db.GetService<IMigrator>().MigrateAsync("20261115183410_SeasonCreationPermissions", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET \"Status\" = {previousStatus}, \"Description\" = 'Keep my profile', \"Capabilities\" = ARRAY['currency.manage']::text[] WHERE \"CommunityId\" = {season.Id} AND \"CharacterId\" = {identity.Character.Id}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM memberships WHERE \"CommunityId\" = {other.Id} AND \"CharacterId\" = {identity.Character.Id}", ct);
        }
        await Migrate(f);
        var seasons = (await SeasonList(player))!.Items;
        Assert.Equal(2, seasons.Length);
        Assert.Equal("75", seasons.Single(x => x.Id == season.Id).MyBalance.Owned);
        Assert.Equal("0", seasons.Single(x => x.Id == other.Id).MyBalance.Owned);
        Assert.Empty(seasons.Single(x => x.Id == season.Id).MyCapabilities);
        var member = await Member(admin, season, identity.Character.Id);
        Assert.Equal("active", member.Status); Assert.Equal("Keep my profile", member.Description);
        await SeasonList(player);
        await using var check = f.Services.CreateAsyncScope(); var database = check.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(4, await database.Memberships.CountAsync(ct));
        Assert.Equal(75, (await database.Memberships.SingleAsync(x => x.CommunityId == season.Id && x.CharacterId == identity.Character.Id, ct)).Owned);
    }

    [Fact]
    public async Task Season_join_leave_removal_and_block_endpoints_are_absent_for_every_role()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = SeasonFactory(); await Migrate(f);
        using var admin = f.CreateClient(); await Login(admin, "90001");
        using var player = f.CreateClient(); var identity = await Login(player, "90022");
        var season = await Draft(admin); await Transition(admin, season);
        var target = $"/api/v2/seasons/{season.Id}/members/{identity.Character.Id}";
        var commands = new[] { (HttpMethod.Post, "/api/v2/seasons/join"), (HttpMethod.Post, $"/api/v2/seasons/{season.Id}/leave"),
            (HttpMethod.Delete, target), (HttpMethod.Post, target + "/removal"), (HttpMethod.Post, target + "/restoration"),
            (HttpMethod.Put, target + "/block"), (HttpMethod.Delete, target + "/block") };
        foreach (var client in new[] { admin, player })
        foreach (var (method, path) in commands)
        {
            using var request = Request(method, path, new { Reason = "No manual season membership" });
            using var response = await client.SendAsync(request, ct);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }
        Assert.Equal(season.Id, Assert.Single((await SeasonList(player))!.Items).Id);
        Assert.Equal("active", (await Member(admin, season, identity.Character.Id)).Status);
    }

    [Fact]
    public async Task Newcomers_open_archived_seasons_but_not_other_players_history_audit_or_private_receipts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = SeasonFactory(); await Migrate(f);
        using var admin = f.CreateClient(); var root = await Login(admin, "90001");
        var season = await Draft(admin); await Transition(admin, season);
        const string privateReason = "Owner financial reason";
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        using var request = Request(HttpMethod.Post, $"/api/v2/seasons/{season.Id}/currency/adjustments", new CurrencyAdjustment(root.Character.Id, "10", privateReason), key);
        using var response = await admin.SendAsync(request, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<CommandResult<Balance>>(ct))!.OperationId;
        await Transition(admin, season, activate: false);
        using var newcomer = f.CreateClient(); await Login(newcomer, "90023");
        var history = (await newcomer.GetFromJsonAsync<HistoryEntryPage>($"/api/v2/seasons/{season.Id}/history", ct))!;
        Assert.Empty(history.Items); Assert.DoesNotContain(history.Items, x => x.Reason == privateReason);
        Assert.Equal(HttpStatusCode.Forbidden, (await newcomer.GetAsync($"/api/v2/seasons/{season.Id}/audit", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await newcomer.GetAsync($"/api/v2/operations/{receipt}", ct)).StatusCode);
        Assert.Equal("0", Assert.Single((await SeasonList(newcomer))!.Items).MyBalance.Owned);
    }
}
