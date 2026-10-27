using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests : PostgresTestDatabase
{
    private readonly FakeDurableProvider provider = new();
    private readonly TestClock clock = new();
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseEnvironment("Testing"); b.UseSetting("ConnectionStrings:Database", Connection);
        b.UseSetting("XivAuth:DurableEnabled", "true"); b.UseSetting("XivAuth:ClientId", "test-app"); b.UseSetting("XivAuth:ClientSecret", "test-secret");
        b.UseSetting("XivAuth:DurableCallbackUrl", "https://server.example/auth/xivauth/callback");
        b.UseSetting("SecretVault:ActiveKeyId", "test"); b.UseSetting("SecretVault:Keys:test", Convert.ToBase64String(new byte[32]));
        b.ConfigureServices(s => { s.RemoveAll<IDurableIdentityProvider>(); s.AddSingleton<IDurableIdentityProvider>(provider); s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); });
    });
    private async Task Migrate(WebApplicationFactory<Program> f) { await using var scope = f.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<AmorDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken); }
    private async Task<SessionTokens> Login(HttpClient c, string id, string owner = "owner", Guid? known = null, bool legacy = false)
    {
        var api = legacy ? "/api/v1" : "/api/v2";
        using var start = Request(HttpMethod.Post, api + "/auth/login-attempts", new LoginStart("Person " + id, "35", "Zalera", known));
        start.Headers.Add("X-Login-Request-Credential", SecretVault.NewSecret());
        using var response = await c.SendAsync(start, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attempt = (await response.Content.ReadFromJsonAsync<CommandResult<LoginAttemptCreated>>(TestContext.Current.CancellationToken))!.Result;
        var state = QueryHelpers.ParseQuery(new Uri(attempt.AuthorizationUrl).Query)["state"];
        using var callback = await c.GetAsync("/auth/xivauth/callback?state=" + state + "&code=" + id + ":" + owner, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        var code = PairingCodeFromPage(await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var exchange = Request(HttpMethod.Post, $"{api}/auth/login-attempts/{attempt.AttemptId}/exchange", new LoginExchangeRequest(code)); exchange.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        using var tokensResponse = await c.SendAsync(exchange, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, tokensResponse.StatusCode);
        var tokens = (await tokensResponse.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken); return tokens;
    }
    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null, Guid? key = null, string? etag = null)
    {
        var r = new HttpRequestMessage(method, path); r.Headers.Add("Idempotency-Key", (key ?? Guid.CreateVersion7(clock.GetUtcNow())).ToString());
        if (body != null) r.Content = JsonContent.Create(body); if (etag != null) r.Headers.Add("If-Match", etag); return r;
    }
    private async Task<T> Command<T>(HttpClient c, HttpMethod method, string path, object? body = null, string? etag = null)
    {
        using var r = Request(method, path, body, etag: etag); using var response = await c.SendAsync(r, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return (await response.Content.ReadFromJsonAsync<CommandResult<T>>(TestContext.Current.CancellationToken))!.Result;
    }
    private Task<Community> Create(HttpClient c, string name = "Test community") => Command<Community>(c, HttpMethod.Post, "/api/v2/communities", new CommunityCreate(name, "RP only", "Coins", "C", 12, 5));
    private Task<InvitationCreated> Invite(HttpClient owner, Community g, int uses = 5) => Command<InvitationCreated>(owner, HttpMethod.Post, $"/api/v2/communities/{g.Id}/invitations", new InvitationCreate(24, uses));
    private Task<Community> Join(HttpClient c, InvitationCreated invite) => Command<Community>(c, HttpMethod.Post, "/api/v2/communities/join", new JoinCommunity(invite.InvitationCode, true));
    private async Task<Member> Member(HttpClient c, Community g, Guid id) => (await c.GetFromJsonAsync<MemberPage>($"/api/v2/communities/{g.Id}/members?includeDormant=true", TestContext.Current.CancellationToken))!.Items.Single(x => x.Character.Id == id);

    [Fact]
    public async Task Login_survives_server_restart_and_refresh_is_single_use_with_safe_same_key_retry()
    {
        SessionTokens original;
        using (var f = Factory()) { await Migrate(f); using var c = f.CreateClient(); original = await Login(c, "100"); await Create(c); }
        using var restarted = Factory(); using var client = restarted.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", original.AccessToken);
        Assert.Single((await client.GetFromJsonAsync<CommunityPage>("/api/v2/communities", TestContext.Current.CancellationToken))!.Items);
        var key = Guid.CreateVersion7();
        using var req = Request(HttpMethod.Post, "/api/v2/auth/sessions/refresh", new RefreshRequest(original.RefreshToken), key);
        using var first = await client.SendAsync(req, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var tokens = (await first.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result;
        using var retry = Request(HttpMethod.Post, "/api/v2/auth/sessions/refresh", new RefreshRequest(original.RefreshToken), key);
        using var replay = await client.SendAsync(retry, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(tokens.RefreshToken, (await replay.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result.RefreshToken);
        using var theft = Request(HttpMethod.Post, "/api/v2/auth/sessions/refresh", new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(theft, TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v2/communities", TestContext.Current.CancellationToken)).StatusCode);
    }
    [Fact]
    public async Task Returning_binding_reuses_internal_character_and_wrong_provider_ownership_cannot_claim_assets()
    {
        using var f = Factory(); await Migrate(f); using var c = f.CreateClient(); var first = await Login(c, "101"); await Create(c);
        var second = await Login(c, "101", known: first.Character.Id); Assert.Equal(first.Character.Id, second.Character.Id); Assert.Equal("101", provider.LastLookupId);
        Assert.Single((await c.GetFromJsonAsync<CommunityPage>("/api/v2/communities", TestContext.Current.CancellationToken))!.Items);
        var api = "/api/v2";
        using var start = Request(HttpMethod.Post, api + "/auth/login-attempts", new LoginStart("Person 101", "35", "Zalera", first.Character.Id)); start.Headers.Add("X-Login-Request-Credential", SecretVault.NewSecret());
        var attempt = (await (await c.SendAsync(start, TestContext.Current.CancellationToken)).Content.ReadFromJsonAsync<CommandResult<LoginAttemptCreated>>(TestContext.Current.CancellationToken))!.Result;
        var state = QueryHelpers.ParseQuery(new Uri(attempt.AuthorizationUrl).Query)["state"];
        await c.GetAsync("/auth/xivauth/callback?state=" + state + "&code=101:different-owner", TestContext.Current.CancellationToken);
        using var poll = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/auth/login-attempts/{attempt.AttemptId}"); poll.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        var status = await (await c.SendAsync(poll, TestContext.Current.CancellationToken)).Content.ReadFromJsonAsync<LoginAttemptStatus>(TestContext.Current.CancellationToken); Assert.Equal("identity_binding_changed", status!.FailureCode);
        Assert.Single((await c.GetFromJsonAsync<CommunityPage>("/api/v2/communities", TestContext.Current.CancellationToken))!.Items);
    }
    [Fact]
    public async Task Concurrent_community_creation_and_joins_enforce_three_owned_six_joined_including_owned()
    {
        using var f = Factory(); await Migrate(f); using var c = f.CreateClient(); await Login(c, "102");
        var creations = Enumerable.Range(0, 6).Select(async i => { using var req = Request(HttpMethod.Post, "/api/v2/communities", new CommunityCreate("Own " + i, "", "Coins", "", 0, 5)); return await c.SendAsync(req, TestContext.Current.CancellationToken); }).ToArray();
        var responses = await Task.WhenAll(creations); Assert.Equal(3, responses.Count(x => x.IsSuccessStatusCode)); Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict)); foreach (var r in responses) r.Dispose();
        var invitations = new List<InvitationCreated>();
        for (var i = 0; i < 2; i++) { using var other = f.CreateClient(); await Login(other, (110+i).ToString()); for (var j=0;j<2;j++) invitations.Add(await Invite(other, await Create(other, $"Other {i}-{j}"))); }
        var joins = invitations.Select(async inv => { using var req = Request(HttpMethod.Post, "/api/v2/communities/join", new JoinCommunity(inv.InvitationCode, true)); return await c.SendAsync(req, TestContext.Current.CancellationToken); }).ToArray();
        var joined = await Task.WhenAll(joins); Assert.Equal(3, joined.Count(x => x.IsSuccessStatusCode)); Assert.Single(joined, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var r in joined) r.Dispose();
        Assert.Equal(6, (await c.GetFromJsonAsync<CommunityPage>("/api/v2/communities", TestContext.Current.CancellationToken))!.Items.Length);
    }
    [Fact]
    public async Task Permanent_invitation_accepts_concurrent_members_and_codes_are_not_listed()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "120"); var community = await Create(owner); var invitation = await Invite(owner, community, 1);
        using var a = f.CreateClient(); using var b = f.CreateClient(); await Login(a, "121"); await Login(b, "122");
        using var ar = Request(HttpMethod.Post, "/api/v2/communities/join", new JoinCommunity(invitation.InvitationCode, true)); using var br = Request(HttpMethod.Post, "/api/v2/communities/join", new JoinCommunity(invitation.InvitationCode, true));
        var result = await Task.WhenAll(a.SendAsync(ar, TestContext.Current.CancellationToken), b.SendAsync(br, TestContext.Current.CancellationToken)); Assert.All(result, x => Assert.True(x.IsSuccessStatusCode)); foreach(var r in result) r.Dispose();
        var listed = await owner.GetStringAsync($"/api/v2/communities/{community.Id}/invitations", TestContext.Current.CancellationToken); Assert.DoesNotContain(invitation.InvitationCode, listed);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(2, (await db.Invitations.SingleAsync(TestContext.Current.CancellationToken)).Used); Assert.DoesNotContain(invitation.InvitationCode, (await db.Operations.FirstAsync(x => x.Kind == "invitation.create", TestContext.Current.CancellationToken)).ProtectedResponse);
    }
    [Fact]
    public async Task Currency_permission_isolation_idempotency_and_concurrent_debits_preserve_ledger()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerToken = await Login(owner, "130"); var a = await Create(owner, "A"); var b = await Create(owner, "B");
        using var member = f.CreateClient(); var memberToken = await Login(member, "131"); await Join(member, await Invite(owner, a));
        using var outsider = f.CreateClient(); var outsiderToken = await Login(outsider, "132"); await Join(outsider, await Invite(owner, b));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/v2/communities/{b.Id}", TestContext.Current.CancellationToken)).StatusCode);
        var adjustment = new CurrencyAdjustment(memberToken.Character.Id, "1000", "Starting balance");
        using var denied = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/currency/adjustments", adjustment); Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(denied, TestContext.Current.CancellationToken)).StatusCode);
        var roster = await Member(owner, a, memberToken.Character.Id);
        await Command<Member>(owner, HttpMethod.Put, $"/api/v2/communities/{a.Id}/members/{memberToken.Character.Id}/capabilities", new MemberCapabilities(["currency.manage"]), roster.Etag);
        var key = Guid.CreateVersion7();
        using var mint = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/currency/adjustments", adjustment, key); Assert.Equal(HttpStatusCode.OK, (await member.SendAsync(mint, TestContext.Current.CancellationToken)).StatusCode);
        using var retry = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/currency/adjustments", adjustment, key); Assert.Equal(HttpStatusCode.OK, (await member.SendAsync(retry, TestContext.Current.CancellationToken)).StatusCode);
        using var mismatch = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/currency/adjustments", adjustment with { Delta = "5" }, key); Assert.Equal(HttpStatusCode.Conflict, (await member.SendAsync(mismatch, TestContext.Current.CancellationToken)).StatusCode);
        using var cross = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/currency/adjustments", new CurrencyAdjustment(outsiderToken.Character.Id, "1", "Cross-community attempt")); Assert.Equal(HttpStatusCode.NotFound, (await member.SendAsync(cross, TestContext.Current.CancellationToken)).StatusCode);
        var debits = Enumerable.Range(0,2).Select(async _ => { using var req = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/currency/adjustments", adjustment with { Delta = "-750", Reason = "Purchase" }); return await member.SendAsync(req, TestContext.Current.CancellationToken); }).ToArray();
        var results = await Task.WhenAll(debits); Assert.Single(results, x => x.IsSuccessStatusCode); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict); foreach(var r in results) r.Dispose();
        Assert.Equal("250", (await member.GetFromJsonAsync<BalancePage>($"/api/v2/communities/{a.Id}/balances", TestContext.Current.CancellationToken))!.Items.Single().Owned);
        var granted = await Member(owner, a, memberToken.Character.Id); await Command<Member>(owner, HttpMethod.Put, $"/api/v2/communities/{a.Id}/members/{memberToken.Character.Id}/capabilities", new MemberCapabilities([]), granted.Etag);
        using var revoked = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/currency/adjustments", adjustment); Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(revoked, TestContext.Current.CancellationToken)).StatusCode);
        var history = (await owner.GetFromJsonAsync<HistoryEntryPage>($"/api/v2/communities/{a.Id}/audit", TestContext.Current.CancellationToken))!.Items;
        Assert.Equal(2, history.Count(x => x.Kind == "currency.adjust")); Assert.Contains(history, x => x.Reason == "Purchase");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/v2/communities/{a.Id}/audit", TestContext.Current.CancellationToken)).StatusCode);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var ledger = await db.Operations.Where(x => x.Kind == "currency.adjust").ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1000, ledger.Single(x => x.Delta == 1000).AfterBalance); Assert.Equal(250, ledger.Single(x => x.Delta == -750).AfterBalance);
    }
    [Fact]
    public async Task Removal_restore_rejoin_preserves_balance_clears_grants_and_policy_changes_wait_until_monday()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "140"); var community = await Create(owner);
        using var member = f.CreateClient(); var tokens = await Login(member, "141"); var invitation = await Invite(owner, community); await Join(member, invitation);
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(tokens.Character.Id, "7", "Gift"));
        var target = await Member(owner, community, tokens.Character.Id);
        target = await Command<Member>(owner, HttpMethod.Put, $"/api/v2/communities/{community.Id}/members/{tokens.Character.Id}/capabilities", new MemberCapabilities(["currency.manage"]), target.Etag);
        target = await Command<Member>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/members/{tokens.Character.Id}/removal", new MemberRemoval("Removed"), target.Etag);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/v2/communities/{community.Id}", TestContext.Current.CancellationToken)).StatusCode);
        using var blocked = Request(HttpMethod.Post, "/api/v2/communities/join", new JoinCommunity(invitation.InvitationCode, true)); Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(blocked, TestContext.Current.CancellationToken)).StatusCode);
        await Command<Member>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/members/{tokens.Character.Id}/restoration", new MemberRemoval("Restored eligibility"), target.Etag);
        var joined = await Join(member, invitation); Assert.Equal("7", joined.MyBalance.Owned); Assert.Empty(joined.MyCapabilities);
        using var policies = await owner.GetAsync($"/api/v2/communities/{community.Id}/policies", TestContext.Current.CancellationToken);
        var changed = await Command<CommunityPolicies>(owner, HttpMethod.Put, $"/api/v2/communities/{community.Id}/policies", new PolicyEdit(20, 9), policies.Headers.ETag!.ToString());
        Assert.Equal(12, changed.Current.PotionPoints); Assert.Equal(20, changed.Next.PotionPoints);
        clock.UtcNow = changed.NextResetAt;
        // Obtain a new login after the ordinary 30-minute access credential expiry.
        await Login(owner, "140"); var next = (await owner.GetFromJsonAsync<CommunityPolicies>($"/api/v2/communities/{community.Id}/policies", TestContext.Current.CancellationToken))!;
        Assert.Equal(20, next.Current.PotionPoints); Assert.Equal(9, next.Current.Letters);
    }
    [Fact]
    public async Task Ownership_requires_recipient_acceptance_and_former_owner_loses_authority()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "150"); var community = await Create(owner);
        using var member = f.CreateClient(); var token = await Login(member, "151"); await Join(member, await Invite(owner, community));
        var transfer = await Command<OwnershipTransfer>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/ownership-transfers", new OwnershipProposal(token.Character.Id));
        using var wrong = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/ownership-transfers/{transfer.Id}/accept"); Assert.Equal(HttpStatusCode.Forbidden, (await owner.SendAsync(wrong, TestContext.Current.CancellationToken)).StatusCode);
        await Command<OwnershipTransfer>(member, HttpMethod.Post, $"/api/v2/communities/{community.Id}/ownership-transfers/{transfer.Id}/accept");
        using var invitation = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/invitations", new InvitationCreate(24,1)); Assert.Equal(HttpStatusCode.Forbidden, (await owner.SendAsync(invitation, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(token.Character.Id, (await member.GetFromJsonAsync<Community>($"/api/v2/communities/{community.Id}", TestContext.Current.CancellationToken))!.OwnerCharacterId);
    }
    [Fact]
    public async Task Version_preconditions_bound_pagination_and_deletion_receipts_keep_management_safe()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var original = await Login(owner, "160"); var community = await Create(owner);
        using var missing = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}", new CommunityEdit("Updated", ""));
        Assert.Equal((HttpStatusCode)428, (await owner.SendAsync(missing, TestContext.Current.CancellationToken)).StatusCode);
        using var stale = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}", new CommunityEdit("Updated", ""), etag: "\"wrong\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await owner.SendAsync(stale, TestContext.Current.CancellationToken)).StatusCode);
        var a = await Invite(owner, community); var b = await Invite(owner, community);
        var first = (await owner.GetFromJsonAsync<InvitationPage>($"/api/v2/communities/{community.Id}/invitations?limit=1", TestContext.Current.CancellationToken))!;
        Assert.NotNull(first.NextCursor);
        var second = (await owner.GetFromJsonAsync<InvitationPage>($"/api/v2/communities/{community.Id}/invitations?limit=1&cursor={Uri.EscapeDataString(first.NextCursor)}", TestContext.Current.CancellationToken))!;
        Assert.NotEqual(first.Items.Single().Id, second.Items.Single().Id);
        var other = await Create(owner, "Other");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.GetAsync($"/api/v2/communities/{other.Id}/invitations?cursor={Uri.EscapeDataString(first.NextCursor)}", TestContext.Current.CancellationToken)).StatusCode);
        using var resource = await owner.GetAsync($"/api/v2/communities/{community.Id}", TestContext.Current.CancellationToken);
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        using var delete = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/deletion", new CommunityDeletion(community.Name), key, resource.Headers.ETag!.ToString());
        using var response = await owner.SendAsync(delete, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v2/communities/{community.Id}", TestContext.Current.CancellationToken)).StatusCode);
        var receipt = await owner.GetFromJsonAsync<Operation>($"/api/v2/operation-keys/{key}", TestContext.Current.CancellationToken);
        Assert.Equal("community.delete", receipt!.Kind);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v2/communities/{community.Id}/history", TestContext.Current.CancellationToken)).StatusCode);
        using var revoke = Request(HttpMethod.Delete, "/api/v2/auth/sessions/current"); Assert.Equal(HttpStatusCode.OK, (await owner.SendAsync(revoke, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/v2/communities", TestContext.Current.CancellationToken)).StatusCode);
    }

}
internal sealed class TestClock : TimeProvider { public DateTimeOffset UtcNow = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => UtcNow; }
internal sealed class FakeDurableProvider : IDurableIdentityProvider
{
    public string? LastLookupId;
    public Func<string, string, CancellationToken, Task<ProviderGrant?>>? Renewal;
    public string AuthorizationUrl(string state, string challenge) => "https://xivauth.net/oauth/authorize?state=" + state;
    public Task<ProviderGrant?> ExchangeAsync(string code, string verifier, LoginStart selected, string? lodestoneId, CancellationToken ct)
    { LastLookupId = lodestoneId; var values = code.Split(':'); return Task.FromResult<ProviderGrant?>(new(new(values[0], values[1], selected.DisplayName, selected.HomeWorldName), code)); }
    public Task<ProviderGrant?> RenewAsync(string refresh, string lodestoneId, CancellationToken ct)
    { if (Renewal != null) return Renewal(refresh, lodestoneId, ct); return Task.FromResult(RenewalGrant(refresh, lodestoneId)); }
    public static ProviderGrant? RenewalGrant(string refresh, string lodestoneId)
    { var values = refresh.Split(':'); return new(new(lodestoneId, values[1], "Person " + lodestoneId, "Zalera"), refresh); }
}
