using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
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
    [Fact]
    public async Task Administrator_has_creation_by_default_and_grants_and_revokes_it_explicitly()
    {
        using var f = SeasonFactory(); await Migrate(f);
        using var admin = f.CreateClient(); var root = await Login(admin, "90001");
        using var ordinary = f.CreateClient(); var playerIdentity = await Login(ordinary, "90011");
        var context = (await admin.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!;
        Assert.Contains(SeasonCapabilities.Create, context.MyCapabilities);
        Assert.Empty((await ordinary.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!.MyCapabilities);
        var body = new SeasonCreate("Denied", "", "Coins", "C", 0, 5);
        using (var request = Request(HttpMethod.Post, "/api/v2/seasons", body))
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
        // Owning an existing season is scoped authority, not global creation permission.
        var existing = await Draft(admin, "Existing season"); await Transition(admin, existing);
        var transfer = await Command<OwnershipTransfer>(admin, HttpMethod.Post, $"/api/v2/seasons/{existing.Id}/ownership-transfers", new OwnershipProposal(playerIdentity.Character.Id));
        await Command<OwnershipTransfer>(ordinary, HttpMethod.Post, $"/api/v2/seasons/{existing.Id}/ownership-transfers/{transfer.Id}/accept");
        Assert.Empty((await ordinary.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!.MyCapabilities);
        using (var request = Request(HttpMethod.Post, "/api/v2/seasons", body))
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
        var page = (await admin.GetFromJsonAsync<SeasonCreationPermissionPage>("/api/v2/seasons/creation-permissions", TestContext.Current.CancellationToken))!;
        var administrator = page.Items.Single(x => x.Character.Id == root.Character.Id);
        Assert.True(administrator.CanCreate);
        using (var revokeDefault = Request(HttpMethod.Put, $"/api/v2/seasons/creation-permissions/{root.Character.Id}", new SeasonCreationPermissionEdit(false, "Cannot revoke administrator default"), etag: administrator.Etag))
            Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(revokeDefault, TestContext.Current.CancellationToken)).StatusCode);
        var selected = page.Items.Single(x => x.Character.Id == playerIdentity.Character.Id);
        var granted = await Command<SeasonCreationPermission>(admin, HttpMethod.Put, $"/api/v2/seasons/creation-permissions/{selected.Character.Id}", new SeasonCreationPermissionEdit(true, "Designated season creator"), selected.Etag);
        Assert.True(granted.CanCreate);
        Assert.Contains(SeasonCapabilities.Create, (await ordinary.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!.MyCapabilities);
        var draft = await Draft(ordinary, "Permitted draft"); Assert.Equal("draft", draft.SeasonState);
        using (var activate = Request(HttpMethod.Post, $"/api/v2/seasons/{draft.Id}/activation", new SeasonTransition(draft.Name), etag: $"\"season:{draft.Id:N}:1\""))
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.SendAsync(activate, TestContext.Current.CancellationToken)).StatusCode);
        using (var selfGrant = Request(HttpMethod.Put, $"/api/v2/seasons/creation-permissions/{selected.Character.Id}", new SeasonCreationPermissionEdit(true, "Self grant"), etag: granted.Etag))
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.SendAsync(selfGrant, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/v2/seasons/creation-permissions", TestContext.Current.CancellationToken)).StatusCode);
        await Command<SeasonCreationPermission>(admin, HttpMethod.Put, $"/api/v2/seasons/creation-permissions/{selected.Character.Id}", new SeasonCreationPermissionEdit(false, "Permission ended"), granted.Etag);
        using (var request = Request(HttpMethod.Post, "/api/v2/seasons", body))
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Old_community_and_api_aliases_are_absent_and_membership_is_automatic()
    {
        using var f = SeasonFactory(); await Migrate(f); using var admin = f.CreateClient(); await Login(admin, "90001");
        foreach (var path in new[] { "/api/v1/capabilities", "/api/v2/communities" })
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        var draft = await Draft(admin); await Transition(admin, draft);
        using var playerClient = f.CreateClient(); await Login(playerClient, "90012");
        Assert.Equal(draft.Id, Assert.Single((await SeasonList(playerClient))!.Items).Id);
        foreach (var path in new[] { "/api/v2/communities", "/api/v2/seasons/join", $"/api/v2/seasons/{draft.Id}/invitations", $"/api/v2/seasons/{draft.Id}/leave" })
        {
            using var request = Request(HttpMethod.Post, path, new { });
            using var response = await playerClient.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }
    }

    [Fact]
    public async Task Migration_converts_existing_records_to_drafts_without_changing_assets_or_identity()
    {
        using var f = SeasonFactory(); await Migrate(f); using var admin = f.CreateClient(); var identity = await Login(admin, "90001");
        var existing = await Create(admin, "Existing world");
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            var member = await db.Memberships.SingleAsync(x => x.CommunityId == existing.Id && x.CharacterId == identity.Character.Id, TestContext.Current.CancellationToken);
            member.Owned = 75; await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.GetService<IMigrator>().MigrateAsync("20261115161226_Seasons", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE communities SET \"SeasonState\" = 'legacy' WHERE \"Id\" = {existing.Id}", TestContext.Current.CancellationToken);
        }
        await Migrate(f);
        var restored = Assert.Single((await SeasonList(admin))!.Items);
        Assert.Equal(existing.Id, restored.Id); Assert.Equal("draft", restored.SeasonState);
        Assert.Equal("75", restored.MyBalance.Owned); Assert.Equal(existing.Currency.Id, restored.Currency.Id);
        Assert.Equal(identity.Character.Id, restored.OwnerCharacterId);
        await Transition(admin, restored);
        Assert.Equal("75", Assert.Single((await SeasonList(admin))!.Items).MyBalance.Owned);
    }
}
