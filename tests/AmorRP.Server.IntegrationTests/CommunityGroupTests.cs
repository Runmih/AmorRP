using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
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
    public async Task Community_owner_delegates_group_creation_and_management_separately_without_privilege_escalation()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "910"); var community = await Create(owner);
        using var member = f.CreateClient(); var identity = await Login(member, "911"); await Join(member, await Invite(owner, community));
        var root = $"/api/v2/communities/{community.Id}/groups"; var body = new GroupWrite("City Watch", "Guarding the community.");
        using (var request = Request(HttpMethod.Post, root, body))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var current = await GrantHome(owner, community, identity.Character.Id, "groups.create");
        var created = await Command<GroupProfile>(member, HttpMethod.Post, root, body);
        Assert.Equal(community.Id, created.CommunityId); Assert.Equal(body.Description, created.Description);
        using (var request = Request(HttpMethod.Patch, root + "/" + created.Id, body with { Description = "Not authorized to edit yet" }, etag: created.Etag))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        // Managing gameplay permissions must not allow self-granting moderator rights.
        current = await GrantHome(owner, community, identity.Character.Id, "members.gameplay.manage");
        using (var request = Request(HttpMethod.Put, $"/api/v2/communities/{community.Id}/members/{identity.Character.Id}/capabilities", new MemberCapabilities(["members.gameplay.manage", "groups.manage"]), etag: current.Etag))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantHome(owner, community, identity.Character.Id, "groups.manage");
        var edited = await Command<GroupProfile>(member, HttpMethod.Patch, root + "/" + created.Id, body with { Description = "Night patrol" }, created.Etag);
        using (var request = Request(HttpMethod.Post, root, body))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var request = Request(HttpMethod.Patch, root + "/" + created.Id, body, etag: created.Etag))
        using (var stale = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await GrantHome(owner, community, identity.Character.Id);
        using (var request = Request(HttpMethod.Delete, root + "/" + created.Id, etag: edited.Etag))
        using (var denied = await member.SendAsync(request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var visible = (await member.GetFromJsonAsync<GroupPage>(root, TestContext.Current.CancellationToken))!; Assert.Single(visible.Items);
        await Command<GroupProfile>(owner, HttpMethod.Delete, root + "/" + created.Id, etag: edited.Etag);
        Assert.Empty((await member.GetFromJsonAsync<GroupPage>(root, TestContext.Current.CancellationToken))!.Items);
    }

    [Fact]
    public async Task Group_profiles_and_logos_are_community_scoped_bounded_and_recover_with_same_operation_key()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "912"); var community = await Create(owner);
        var root = $"/api/v2/communities/{community.Id}/groups"; var key = Guid.CreateVersion7(clock.GetUtcNow()); var body = new GroupWrite("Arena team", new string('x', 300));
        GroupProfile created;
        using (var request = Request(HttpMethod.Post, root, body, key))
        using (var response = await owner.SendAsync(request, ct)) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); created = (await response.Content.ReadFromJsonAsync<CommandResult<GroupProfile>>(ct))!.Result; }
        using (var request = Request(HttpMethod.Post, root, body, key))
        using (var response = await owner.SendAsync(request, ct)) { Assert.True(response.IsSuccessStatusCode); Assert.Equal("true", response.Headers.GetValues("Idempotency-Replayed").Single()); Assert.Equal(created.Id, (await response.Content.ReadFromJsonAsync<CommandResult<GroupProfile>>(ct))!.Result.Id); }
        foreach (var bad in new[] { body with { Description = new string('x', 301) }, body with { Name = new string('x', 81) }, body with { Name = "\u0001" } })
        {
            using var request = Request(HttpMethod.Patch, root + "/" + created.Id, bad, etag: created.Etag); using var denied = await owner.SendAsync(request, ct); Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
        }
        var logoPath = root + "/" + created.Id + "/logo";
        using var uploaded = await UploadCommunityImage(owner, logoPath, LogoBytes(1024, 512), $"\"group-logo:{created.Id:N}:{created.LogoVersion}\"");
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var withLogo = (await uploaded.Content.ReadFromJsonAsync<CommandResult<GroupProfile>>(ct))!.Result; Assert.NotNull(withLogo.LogoAssetId);
        using var image = await owner.GetAsync(logoPath, ct); Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        using var stranger = f.CreateClient(); await Login(stranger, "913"); var other = await Create(stranger);
        using (var denied = await stranger.GetAsync(logoPath, ct)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var wrongParent = await stranger.GetAsync($"/api/v2/communities/{other.Id}/groups/{created.Id}", ct)) Assert.Equal(HttpStatusCode.NotFound, wrongParent.StatusCode);
        using (var before = await owner.GetAsync($"/api/v2/communities/{community.Id}/logo", ct)) Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        await Command<GroupProfile>(owner, HttpMethod.Delete, root + "/" + created.Id, etag: withLogo.Etag);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.False(await db.MediaAssets.AnyAsync(x => x.Id == withLogo.LogoAssetId, ct));
        Assert.True(await db.Groups.AnyAsync(x => x.Id == created.Id && x.Deleted, ct));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Stable_v1_clients_keep_login_old_field_names_moderation_and_idempotent_writes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var stable = f.CreateClient(); await Login(stable, "914", legacy: true);
        var capabilities = (await stable.GetFromJsonAsync<JsonElement>("/api/v1/capabilities", ct));
        Assert.Equal("1", capabilities.GetProperty("apiVersion").GetString()); Assert.Equal(3, capabilities.GetProperty("limits").GetProperty("ownedGroups").GetInt32());
        Assert.Contains(capabilities.GetProperty("grantableCapabilities").EnumerateArray(), x => x.GetString() == "group.moderate");
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var body = new CommunityCreate("Stable client", "Keep group.moderate as description text", "Coins", "C", 12, 5);
        string original; Guid id; string etag;
        using (var request = Request(HttpMethod.Post, "/api/v1/groups", body, key))
        using (var response = await stable.SendAsync(request, ct))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode); original = await response.Content.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(original); var result = json.RootElement.GetProperty("result"); id = result.GetProperty("id").GetGuid();
            Assert.Equal(id, result.GetProperty("currency").GetProperty("groupId").GetGuid());
            Assert.False(result.GetProperty("currency").TryGetProperty("communityId", out _));
            Assert.Equal(body.Description, result.GetProperty("description").GetString()); etag = response.Headers.ETag!.ToString(); Assert.StartsWith("\"group:", etag);
        }
        using (var request = Request(HttpMethod.Post, "/api/v1/groups", body, key))
        using (var response = await stable.SendAsync(request, ct)) { Assert.Equal(original, await response.Content.ReadAsStringAsync(ct)); Assert.Equal("true", response.Headers.GetValues("Idempotency-Replayed").Single()); }
        using (var edit = Request(HttpMethod.Patch, $"/api/v1/groups/{id}", new CommunityEdit("Renamed by stable", "Description"), etag: etag))
        using (var response = await stable.SendAsync(edit, ct)) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = await stable.GetFromJsonAsync<Community>($"/api/v2/communities/{id}", ct); Assert.Equal("Renamed by stable", current!.Name); Assert.Equal(id, current.Currency.CommunityId);
    }

    [Fact]
    public async Task Rename_migration_preserves_data_assets_sessions_permissions_and_old_receipt_fingerprints()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var identity = await Login(owner, "915"); var community = await Create(owner);
        using var member = f.CreateClient(); var other = await Login(member, "916"); await Join(member, await Invite(owner, community));
        await GrantHome(owner, community, other.Character.Id, "community.moderate");
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(other.Character.Id, "123", "Test funds"));
        var bytes = LogoBytes(128, 128);
        using var upload = await UploadCommunityImage(owner, $"/api/v2/communities/{community.Id}/logo", bytes, LogoTag(community)); Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var saved = (await upload.Content.ReadFromJsonAsync<CommandResult<Community>>(ct))!.Result;
        var oldKey = Guid.CreateVersion7(clock.GetUtcNow()); var oldBody = new CommunityEdit(community.Name, "Preserved profile");
        var oldTag = $"\"group:{community.Id:N}:{saved.Version}\"";
        string oldResponse;
        using (var request = Request(HttpMethod.Patch, $"/api/v1/groups/{community.Id}", oldBody, oldKey, oldTag))
        using (var response = await owner.SendAsync(request, ct)) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); oldResponse = await response.Content.ReadAsStringAsync(ct); }
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            var vault = scope.ServiceProvider.GetRequiredService<SecretVault>();
            var oldOperation = await db.Operations.SingleAsync(x => x.Key == oldKey, ct);
            oldOperation.ProtectedResponse = vault.Protect(LegacyCommunityJson.ToLegacy(vault.Unprotect(oldOperation.ProtectedResponse, "operation:" + oldOperation.Id)), "operation:" + oldOperation.Id);
            oldOperation.ETag = LegacyCommunityJson.ETag(oldOperation.ETag!, legacy: true);
            oldOperation.Kind = oldOperation.Kind.Replace("community.", "group.", StringComparison.Ordinal);
            await db.SaveChangesAsync(ct);
            await db.GetService<IMigrator>().MigrateAsync("20261016220000_PermanentInvitations", ct);
            // Assert against the actual stable schema before upgrading it again.
            await using var cmd = db.Database.GetDbConnection().CreateCommand(); await db.Database.OpenConnectionAsync(ct);
            cmd.CommandText = "SELECT count(*) FROM groups WHERE \"Id\" = @id"; var p = cmd.CreateParameter(); p.ParameterName = "id"; p.Value = community.Id; cmd.Parameters.Add(p); Assert.Equal(1L, await cmd.ExecuteScalarAsync(ct));
            await db.Database.MigrateAsync(ct); db.ChangeTracker.Clear();
            Assert.Equal(123, (await db.Memberships.SingleAsync(x => x.CharacterId == other.Character.Id, ct)).Owned);
            Assert.Contains("community.moderate", (await db.Memberships.SingleAsync(x => x.CharacterId == other.Character.Id, ct)).Capabilities);
            Assert.Equal(saved.LogoAssetId, (await db.Communities.SingleAsync(ct)).LogoAssetId);
            Assert.Equal("community_logo", (await db.MediaAssets.SingleAsync(ct)).Purpose);
            Assert.Equal(2, await db.Sessions.CountAsync(x => !x.Revoked, ct)); Assert.Empty(await db.Groups.ToArrayAsync(ct));
        }
        using (var retry = Request(HttpMethod.Patch, $"/api/v1/groups/{community.Id}", oldBody, oldKey, oldTag))
        using (var replay = await owner.SendAsync(retry, ct)) { Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single()); Assert.Equal(oldResponse, await replay.Content.ReadAsStringAsync(ct)); }
        using var legacy = await owner.GetAsync($"/api/v1/groups/{community.Id}/logo", ct); Assert.Equal(HttpStatusCode.OK, legacy.StatusCode); Assert.Equal(bytes, await legacy.Content.ReadAsByteArrayAsync(ct));
        var visible = (await member.GetFromJsonAsync<CommunityPage>("/api/v2/communities", ct))!.Items; Assert.Equal(community.Id, Assert.Single(visible).Id);
    }
    [Fact]
    public async Task Existing_deployment_policy_overrides_keep_their_values_after_the_rename()
    {
        using var factory = Factory().WithWebHostBuilder(b => b.UseSetting("ServicePolicy:OwnedGroups", "1"));
        using var client = factory.CreateClient();
        var capabilities = await client.GetFromJsonAsync<CapabilitiesResponse>("/api/v2/capabilities", TestContext.Current.CancellationToken);
        Assert.Equal(1, capabilities!.Limits.OwnedCommunities);
    }
}
