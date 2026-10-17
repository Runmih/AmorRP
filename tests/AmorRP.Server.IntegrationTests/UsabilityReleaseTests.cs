using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
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
    public async Task Nine_character_invitations_ignore_legacy_limits_accept_lowercase_and_stop_when_deleted()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "880"); var community = await Create(owner);
        var invite = await Command<InvitationCreated>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/invitations", new InvitationCreate(1, 1));
        Assert.Equal(9, invite.InvitationCode.Length);
        Assert.All(invite.InvitationCode, c => Assert.Contains(c, "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"));
        Assert.Equal(9999, invite.Invitation.ExpiresAt.Year); Assert.Equal(int.MaxValue, invite.Invitation.MaxUses);
        clock.UtcNow = clock.UtcNow.AddDays(30);
        // Renew the owner after moving time beyond the session expiry.
        await Login(owner, "880");
        using var a = f.CreateClient(); await Login(a, "881"); await Join(a, invite with { InvitationCode = " " + invite.InvitationCode.ToLowerInvariant() + " " });
        using var b = f.CreateClient(); await Login(b, "882"); await Join(b, invite);
        using var remove = Request(HttpMethod.Delete, $"/api/v2/communities/{community.Id}/invitations/{invite.Invitation.Id}");
        using var removed = await owner.SendAsync(remove, TestContext.Current.CancellationToken); Assert.True(removed.IsSuccessStatusCode);
        using var c = f.CreateClient(); await Login(c, "883");
        using var join = Request(HttpMethod.Post, "/api/v2/communities/join", new JoinCommunity(invite.InvitationCode, true));
        using var denied = await c.SendAsync(join, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
    }

    [Fact]
    public async Task Member_search_filters_before_pagination_matches_world_and_does_not_cross_communities()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "884"); var community = await Create(owner);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            for (var i = 0; i < 75; i++)
            {
                var character = new CharacterRow { LodestoneId = (99000 + i).ToString(), OwnershipKeyHash = "test-" + i,
                    DisplayName = i >= 70 ? "Distant healer " + i : "Other " + i, HomeWorldId = "35", HomeWorldName = i == 74 ? "World_%" : "Zalera", VerifiedAt = clock.UtcNow };
                db.Characters.Add(character); db.Memberships.Add(new MembershipRow { CommunityId = community.Id, CharacterId = character.Id });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var page = (await owner.GetFromJsonAsync<MemberPage>($"/api/v2/communities/{community.Id}/members?search=distant&limit=1", TestContext.Current.CancellationToken))!;
        Assert.Single(page.Items); Assert.Contains("Distant healer", page.Items[0].Character.DisplayName); Assert.NotNull(page.NextCursor);
        var next = (await owner.GetFromJsonAsync<MemberPage>($"/api/v2/communities/{community.Id}/members?search=distant&limit=1&cursor={Uri.EscapeDataString(page.NextCursor)}", TestContext.Current.CancellationToken))!;
        Assert.Single(next.Items); Assert.NotEqual(page.Items[0].Character.Id, next.Items[0].Character.Id);
        using var mismatch = await owner.GetAsync($"/api/v2/communities/{community.Id}/members?search=other&cursor={Uri.EscapeDataString(page.NextCursor)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);
        var world = (await owner.GetFromJsonAsync<MemberPage>($"/api/v2/communities/{community.Id}/members?search=World_%25", TestContext.Current.CancellationToken))!;
        Assert.Single(world.Items); Assert.Equal("World_%", world.Items[0].Character.HomeWorldName);
        using var stranger = f.CreateClient(); await Login(stranger, "885");
        using var hidden = await stranger.GetAsync($"/api/v2/communities/{community.Id}/members?search=distant", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Invitation_upgrade_preserves_revocation_and_does_not_revive_expired_or_exhausted_codes()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "886"); var community = await Create(owner);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261016140911_GroupLogos", TestContext.Current.CancellationToken);
        var active = new InvitationRow { CommunityId = community.Id, CodeHash = "active", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), MaxUses = 1 };
        var expired = new InvitationRow { CommunityId = community.Id, CodeHash = "expired", ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), MaxUses = 1 };
        var revoked = new InvitationRow { CommunityId = community.Id, CodeHash = "revoked", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), MaxUses = 1, Revoked = true };
        var exhausted = new InvitationRow { CommunityId = community.Id, CodeHash = "exhausted", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), MaxUses = 1, Used = 1 };
        foreach (var row in new[] { active, expired, revoked, exhausted }) await LegacyInsert(db, row, TestContext.Current.CancellationToken);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken); db.ChangeTracker.Clear();
        var rows = await db.Invitations.ToDictionaryAsync(x => x.CodeHash, TestContext.Current.CancellationToken);
        Assert.Equal(9999, rows["active"].ExpiresAt.Year); Assert.Equal(int.MaxValue, rows["active"].MaxUses);
        Assert.True(rows["revoked"].Revoked); Assert.Equal(1, rows["revoked"].MaxUses);
        Assert.True(rows["expired"].ExpiresAt < DateTimeOffset.UtcNow); Assert.Equal(1, rows["exhausted"].MaxUses);
    }
}
