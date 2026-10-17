using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Inventory;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

// Reuses the real PostgreSQL + DI-only provider fixture and stable character login.
public sealed partial class FoundationTests
{
    private async Task<Definition> Recipe(HttpClient c, Community g, int cost = 3, string message = "drinks a healing potion.")
    {
        var cats = (await c.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{g.Id}/categories", TestContext.Current.CancellationToken))!.Items;
        return await Command<Definition>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/item-definitions", new DefinitionWrite("potion", "Healing potion", cats.Single(x => x.AllowedTypeIds.Contains("potion")).Id, "A modest healing potion.", true, new(cost, message)));
    }
    private async Task GrantPotions(HttpClient owner, Community g, Guid id)
    {
        var member = await Member(owner, g, id);
        await Command<AmorRP.Contracts.Communities.Member>(owner, HttpMethod.Put, $"/api/v2/communities/{g.Id}/members/{id}/capabilities", new MemberCapabilities(["items.potion.create"]), member.Etag);
    }
    [Fact]
    public async Task Stale_grant_is_not_saved_and_reloaded_grant_is_visible_to_member_without_relogin()
    {
        using var f = Factory(); await Migrate(f);
        using var owner = f.CreateClient(); await Login(owner, "320"); var g = await Create(owner); var recipe = await Recipe(owner, g);
        using var member = f.CreateClient(); var identity = await Login(member, "321"); await Join(member, await Invite(owner, g));
        var selected = await Member(owner, g, identity.Character.Id);
        await Command<AmorRP.Contracts.Communities.Member>(owner, HttpMethod.Put, $"/api/v2/communities/{g.Id}/members/{identity.Character.Id}/trade-restriction", new MemberRestriction(false, "Changed while form was open"), selected.Etag);
        var path = $"/api/v2/communities/{g.Id}/members/{identity.Character.Id}/capabilities";
        var grant = new MemberCapabilities(["items.potion.create"]);
        using var staleRequest = Request(HttpMethod.Put, path, grant, etag: selected.Etag);
        using var stale = await owner.SendAsync(staleRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Empty((await Member(owner, g, identity.Character.Id)).Capabilities);
        var before = (await owner.GetFromJsonAsync<AmorRP.Contracts.History.HistoryEntryPage>($"/api/v2/communities/{g.Id}/audit", TestContext.Current.CancellationToken))!.Items;
        Assert.DoesNotContain(before, x => x.Kind == "member.capabilities");

        var reloaded = await Member(owner, g, identity.Character.Id); var key = Guid.CreateVersion7(clock.GetUtcNow());
        using var saveRequest = Request(HttpMethod.Put, path, grant, key, reloaded.Etag);
        using var saved = await owner.SendAsync(saveRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var receipt = (await saved.Content.ReadFromJsonAsync<CommandResult<AmorRP.Contracts.Communities.Member>>(TestContext.Current.CancellationToken))!;
        Assert.Contains("items.potion.create", receipt.Result.Capabilities);
        Assert.Contains("items.potion.create", (await Member(owner, g, identity.Character.Id)).Capabilities);
        var direct = (await member.GetFromJsonAsync<Community>($"/api/v2/communities/{g.Id}", TestContext.Current.CancellationToken))!;
        var list = (await member.GetFromJsonAsync<CommunityPage>("/api/v2/communities", TestContext.Current.CancellationToken))!.Items.Single(x => x.Id == g.Id);
        Assert.Contains("items.potion.create", direct.MyCapabilities); Assert.Equal(direct.MyCapabilities, list.MyCapabilities);
        await Command<ProductionResult>(member, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, 1));
        using var retryRequest = Request(HttpMethod.Put, path, grant, key, reloaded.Etag);
        using var retry = await owner.SendAsync(retryRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode); Assert.True(retry.Headers.Contains("Idempotency-Replayed"));
        var audit = (await owner.GetFromJsonAsync<AmorRP.Contracts.History.HistoryEntryPage>($"/api/v2/communities/{g.Id}/audit", TestContext.Current.CancellationToken))!.Items;
        var eventRow = Assert.Single(audit, x => x.Kind == "member.capabilities");
        Assert.Equal(receipt.OperationId, eventRow.OperationId); Assert.Equal(identity.Character.Id, eventRow.SubjectCharacterId);
    }
    [Fact]
    public async Task Concurrent_production_is_budgeted_atomic_and_same_key_use_does_not_consume_twice()
    {
        using var f = Factory(); await Migrate(f); using var c = f.CreateClient(); await Login(c, "300"); var g = await Create(c); var recipe = await Recipe(c, g);
        var tasks = Enumerable.Range(0, 8).Select(async _ => { using var request = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1)); return await c.SendAsync(request, TestContext.Current.CancellationToken); });
        var outcomes = await Task.WhenAll(tasks); Assert.Equal(4, outcomes.Count(x => x.IsSuccessStatusCode)); Assert.Equal(4, outcomes.Count(x => x.StatusCode == HttpStatusCode.Conflict)); foreach (var result in outcomes) result.Dispose();
        var items = (await c.GetFromJsonAsync<HoldingPage>($"/api/v2/communities/{g.Id}/inventory", TestContext.Current.CancellationToken))!.Items; Assert.Equal(4, Assert.Single(items).Quantity);
        Assert.Equal(12, (await c.GetFromJsonAsync<Quotas>($"/api/v2/communities/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "potion_points").Used);
        var holding = items[0]; var key = Guid.CreateVersion7(clock.GetUtcNow()); var use = new ConsumptionRequest(holding.Version, true, "emote");
        using var firstRequest = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/{holding.Id}/use", use, key);
        using var first = await c.SendAsync(firstRequest, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var replayRequest = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/{holding.Id}/use", use, key);
        using var replay = await c.SendAsync(replayRequest, TestContext.Current.CancellationToken); Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(3, (await c.GetFromJsonAsync<HoldingDetails>($"/api/v2/communities/{g.Id}/inventory/{holding.Id}", TestContext.Current.CancellationToken))!.Holding.Quantity);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); Assert.Equal(4, await db.Operations.CountAsync(x => x.Kind == "potion.create", TestContext.Current.CancellationToken)); Assert.Equal(1, await db.Operations.CountAsync(x => x.Kind == "potion.use", TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Recipe_revisions_preserve_owned_messages_and_stale_cost_previews_fail()
    {
        using var f = Factory(); await Migrate(f); using var c = f.CreateClient(); await Login(c, "301"); var g = await Create(c); var recipe = await Recipe(c, g);
        var old = await Command<ProductionResult>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1));
        var newer = await Command<Definition>(c, HttpMethod.Patch, $"/api/v2/communities/{g.Id}/item-definitions/{recipe.Id}", new DefinitionWrite("potion", "Greater healing potion", recipe.CategoryId, "More potent.", true, new(6, "drinks a stronger potion.")), $"\"definition:{recipe.Id:N}:1\"");
        using var stale = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1)); Assert.Equal(HttpStatusCode.Conflict, (await c.SendAsync(stale, TestContext.Current.CancellationToken)).StatusCode);
        var fresh = await Command<ProductionResult>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, newer.Revision, 1)); Assert.NotEqual(old.Holding.Id, fresh.Holding.Id);
        var used = await Command<ConsumptionResult>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/{old.Holding.Id}/use", new ConsumptionRequest(old.Holding.Version, true, "say")); Assert.Equal(recipe.Potion.UseMessage, used.Message);
        Assert.Equal(9, (await c.GetFromJsonAsync<Quotas>($"/api/v2/communities/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "potion_points").Used);
    }
    [Fact]
    public async Task Per_character_quota_survives_leave_rejoin_and_rolls_to_scheduled_week()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "302"); var g = await Create(owner); var recipe = await Recipe(owner, g, 12);
        using var a = f.CreateClient(); var ta = await Login(a, "303"); using var b = f.CreateClient(); var tb = await Login(b, "304"); var inv = await Invite(owner, g); await Join(a, inv); await Join(b, inv); await GrantPotions(owner, g, ta.Character.Id); await GrantPotions(owner, g, tb.Character.Id);
        await Command<ProductionResult>(a, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1));
        Assert.Equal(12, (await b.GetFromJsonAsync<Quotas>($"/api/v2/communities/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "potion_points").Remaining);
        using var leave = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/leave"); Assert.Equal(HttpStatusCode.OK, (await a.SendAsync(leave, TestContext.Current.CancellationToken)).StatusCode); await Join(a, inv); await GrantPotions(owner, g, ta.Character.Id);
        Assert.Equal(0, (await a.GetFromJsonAsync<Quotas>($"/api/v2/communities/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "potion_points").Remaining);
        await Command<CommunityPolicies>(owner, HttpMethod.Put, $"/api/v2/communities/{g.Id}/policies", new PolicyEdit(24, 2), $"\"policy:{g.Id:N}:1\"");
        clock.UtcNow = AmorRP.Server.Features.Communities.CommunityAccess.Period(clock.UtcNow).AddDays(7).AddMinutes(1);
        // Access expiry is independent; renew the saved verified character without creating a new identity.
        await Login(a, "303", known: ta.Character.Id);
        var next = (await a.GetFromJsonAsync<Quotas>($"/api/v2/communities/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "potion_points"); Assert.Equal(24, next.Remaining); Assert.Equal(0, next.Used);
    }
    [Fact]
    public async Task Letter_quota_private_contents_edits_and_discard_do_not_refund()
    {
        using var f = Factory(); await Migrate(f); using var c = f.CreateClient(); var tc = await Login(c, "305"); var g = await Create(c);
        using var member = f.CreateClient(); await Login(member, "306"); await Join(member, await Invite(c, g));
        var category = (await c.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{g.Id}/categories", TestContext.Current.CancellationToken))!.Items.Single(x => x.AllowedTypeIds.Contains("letter"));
        var results = new List<ProductionResult>();
        for (var i = 0; i < 5; i++) results.Add(await Command<ProductionResult>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/letters", new LetterCreate(category.Id, "Letter " + i, "Private secret\nSecond line")));
        var memberLetter = await Command<ProductionResult>(member, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/letters", new LetterCreate(category.Id, "Member letter", "Owner must not read this body"));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v2/communities/{g.Id}/letters/{memberLetter.Holding.LetterId}", TestContext.Current.CancellationToken)).StatusCode);
        var summaries = await c.GetStringAsync($"/api/v2/communities/{g.Id}/members/{memberLetter.Holding.CharacterId}/holdings", TestContext.Current.CancellationToken);
        Assert.Contains("Member letter", summaries); Assert.DoesNotContain("Owner must not read this body", summaries);
        using var exhausted = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/letters", new LetterCreate(category.Id, "Too many", "")); Assert.Equal(HttpStatusCode.Conflict, (await c.SendAsync(exhausted, TestContext.Current.CancellationToken)).StatusCode);
        var holding = results[0].Holding;
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/v2/communities/{g.Id}/letters/{holding.LetterId}", TestContext.Current.CancellationToken)).StatusCode);
        var letter = await c.GetFromJsonAsync<Letter>($"/api/v2/communities/{g.Id}/letters/{holding.LetterId}", TestContext.Current.CancellationToken); Assert.Equal(tc.Character.Id, letter!.Author.Id);
        var edited = await Command<Letter>(c, HttpMethod.Patch, $"/api/v2/communities/{g.Id}/letters/{holding.LetterId}", new LetterEdit("Edited", "Different secret"), $"\"letter:{holding.LetterId:N}:1\""); Assert.Equal(2, edited.Version);
        var current = (await c.GetFromJsonAsync<HoldingDetails>($"/api/v2/communities/{g.Id}/inventory/{holding.Id}", TestContext.Current.CancellationToken))!.Holding;
        await Command<Holding>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/{holding.Id}/discard", new DiscardRequest(1, current.Version));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v2/communities/{g.Id}/letters/{holding.LetterId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(5, (await c.GetFromJsonAsync<Quotas>($"/api/v2/communities/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "letters").Used);
        var history = await c.GetStringAsync($"/api/v2/communities/{g.Id}/audit", TestContext.Current.CancellationToken); Assert.DoesNotContain("Private secret", history); Assert.DoesNotContain("Different secret", history);
    }
    [Fact]
    public async Task Cross_community_definitions_grant_revocation_and_unsafe_chat_are_rejected()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "307"); var a = await Create(owner); var b = await Create(owner); var recipe = await Recipe(owner, a);
        using var member = f.CreateClient(); var tm = await Login(member, "308"); await Join(member, await Invite(owner, a)); await GrantPotions(owner, a, tm.Character.Id);
        using var foreign = Request(HttpMethod.Post, $"/api/v2/communities/{b.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1)); Assert.Equal(HttpStatusCode.NotFound, (await owner.SendAsync(foreign, TestContext.Current.CancellationToken)).StatusCode);
        var created = await Command<ProductionResult>(member, HttpMethod.Post, $"/api/v2/communities/{a.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1));
        var row = await Member(owner, a, tm.Character.Id); await Command<AmorRP.Contracts.Communities.Member>(owner, HttpMethod.Put, $"/api/v2/communities/{a.Id}/members/{tm.Character.Id}/capabilities", new MemberCapabilities([]), row.Etag);
        using var denied = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1)); Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(denied, TestContext.Current.CancellationToken)).StatusCode);
        foreach (var request in new[] { new ConsumptionRequest(created.Holding.Version, false, "say"), new ConsumptionRequest(created.Holding.Version, true, "tell:Someone") })
        { using var invalid = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/inventory/{created.Holding.Id}/use", request); Assert.Equal(HttpStatusCode.UnprocessableEntity, (await member.SendAsync(invalid, TestContext.Current.CancellationToken)).StatusCode); }
        foreach (var message in new[] { "text\n/say injection", new string('a', 51), "uses <t>'s potion", "invisible\u202Etext" })
        { using var unsafeRecipe = Request(HttpMethod.Post, $"/api/v2/communities/{a.Id}/item-definitions", new DefinitionWrite("potion", "Unsafe", recipe.CategoryId, "", true, new(1, message))); Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.SendAsync(unsafeRecipe, TestContext.Current.CancellationToken)).StatusCode); }
        Assert.Equal(1, (await member.GetFromJsonAsync<HoldingDetails>($"/api/v2/communities/{a.Id}/inventory/{created.Holding.Id}", TestContext.Current.CancellationToken))!.Holding.Quantity);
    }
    [Fact]
    public async Task Removal_requires_reason_and_only_success_logs_and_revokes_access_with_assets_retained()
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "309"); var g = await Create(owner); using var member = f.CreateClient(); var tm = await Login(member, "310"); await Join(member, await Invite(owner, g));
        var cat = (await member.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{g.Id}/categories", TestContext.Current.CancellationToken))!.Items.Single(x => x.AllowedTypeIds.Contains("letter"));
        var item = await Command<ProductionResult>(member, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/letters", new LetterCreate(cat.Id, "Keep", "Private"));
        var row = await Member(owner, g, tm.Character.Id);
        using var bad = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/members/{tm.Character.Id}/removal", new MemberRemoval(""), etag: row.Etag); using var rejected = await owner.SendAsync(bad, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode); Assert.Contains("requestId", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var kick = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/members/{tm.Character.Id}/removal", new MemberRemoval("M3 kick check"), etag: row.Etag); Assert.Equal(HttpStatusCode.OK, (await owner.SendAsync(kick, TestContext.Current.CancellationToken)).StatusCode);
        Assert.DoesNotContain((await owner.GetFromJsonAsync<MemberPage>($"/api/v2/communities/{g.Id}/members", TestContext.Current.CancellationToken))!.Items, x => x.Character.Id == tm.Character.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/v2/communities/{g.Id}/inventory", TestContext.Current.CancellationToken)).StatusCode);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); Assert.Equal(1, await db.Operations.CountAsync(x => x.Kind == "member.remove", TestContext.Current.CancellationToken)); Assert.Equal(1, (await db.Holdings.SingleAsync(x => x.Id == item.Holding.Id, TestContext.Current.CancellationToken)).Quantity);
    }
    [Theory]
    [InlineData(SKEncodedImageFormat.Png)] [InlineData(SKEncodedImageFormat.Jpeg)] [InlineData(SKEncodedImageFormat.Webp)]
    public async Task Currency_icons_normalize_persist_isolate_and_replay(SKEncodedImageFormat format)
    {
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var to = await Login(owner, "311"); var g = await Create(owner); using var member = f.CreateClient(); await Login(member, "312"); await Join(member, await Invite(owner, g)); using var outsider = f.CreateClient(); await Login(outsider, "313");
        using var bitmap = new SKBitmap(128, 128); bitmap.Erase(SKColors.Gold); using var encoded = bitmap.Encode(format, 90); var bytes = encoded.ToArray(); var key = Guid.CreateVersion7(clock.GetUtcNow());
        HttpRequestMessage Upload(HttpClient _, Guid opKey, byte[] image, string etag)
        { var req = Request(HttpMethod.Put, $"/api/v2/communities/{g.Id}/currency/icon", key: opKey, etag: etag); var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent(image), "file", "misleading.extension"); req.Content = content; return req; }
        using var forbidden = Upload(member, Guid.CreateVersion7(clock.GetUtcNow()), bytes, $"\"currency:{g.Currency.Id:N}:1\""); Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(forbidden, TestContext.Current.CancellationToken)).StatusCode);
        using var first = Upload(owner, key, bytes, $"\"currency:{g.Currency.Id:N}:1\""); using var uploaded = await owner.SendAsync(first, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        using var replay = Upload(owner, key, bytes, $"\"currency:{g.Currency.Id:N}:1\""); using var repeated = await owner.SendAsync(replay, TestContext.Current.CancellationToken); Assert.True(repeated.Headers.Contains("Idempotency-Replayed"));
        var png = await member.GetByteArrayAsync($"/api/v2/communities/{g.Id}/currency/icon", TestContext.Current.CancellationToken); Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]); Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v2/communities/{g.Id}/currency/icon", TestContext.Current.CancellationToken)).StatusCode);
        using var tooBig = new SKBitmap(129, 128); using var tooBigImage = tooBig.Encode(SKEncodedImageFormat.Png, 100); using var oversized = Upload(owner, Guid.CreateVersion7(clock.GetUtcNow()), tooBigImage.ToArray(), $"\"currency:{g.Currency.Id:N}:2\""); Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.SendAsync(oversized, TestContext.Current.CancellationToken)).StatusCode);
        using var malformed = Upload(owner, Guid.CreateVersion7(clock.GetUtcNow()), [1, 2, 3, 4], $"\"currency:{g.Currency.Id:N}:2\""); Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await owner.SendAsync(malformed, TestContext.Current.CancellationToken)).StatusCode);
        using var restarted = Factory(); using var after = restarted.CreateClient(); after.DefaultRequestHeaders.Authorization = new("Bearer", to.AccessToken); Assert.Equal(png, await after.GetByteArrayAsync($"/api/v2/communities/{g.Id}/currency/icon", TestContext.Current.CancellationToken));
        await Command<AmorRP.Contracts.Currency.Currency>(owner, HttpMethod.Delete, $"/api/v2/communities/{g.Id}/currency/icon", etag: $"\"currency:{g.Currency.Id:N}:2\""); Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/v2/communities/{g.Id}/currency/icon", TestContext.Current.CancellationToken)).StatusCode);
    }
    [Fact]
    public async Task Inventory_search_sort_and_cursors_return_all_summaries_without_private_bodies()
    {
        using var f = Factory(); await Migrate(f); using var c = f.CreateClient(); await Login(c, "314"); var g = await Create(c); var cats = (await c.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{g.Id}/categories", TestContext.Current.CancellationToken))!.Items;
        foreach (var title in new[] { "Zulu", "Alpha", "Alpha" }) await Command<ProductionResult>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/letters", new LetterCreate(cats.Single(x => x.AllowedTypeIds.Contains("letter")).Id, title, "Hidden body"));
        var all = new List<Holding>(); string? cursor = null;
        do { var page = await c.GetFromJsonAsync<HoldingPage>($"/api/v2/communities/{g.Id}/inventory?limit=1&sort=name" + (cursor == null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), TestContext.Current.CancellationToken); all.AddRange(page!.Items); cursor = page.NextCursor; } while (cursor != null);
        Assert.Equal(new[] { "Alpha", "Alpha", "Zulu" }, all.Select(x => x.Name)); Assert.Equal(3, all.Select(x => x.Id).Distinct().Count());
        var raw = await c.GetStringAsync($"/api/v2/communities/{g.Id}/inventory?search=alpha&typeId=letter", TestContext.Current.CancellationToken); Assert.DoesNotContain("Hidden body", raw);
        Assert.Equal(2, (await c.GetFromJsonAsync<HoldingPage>($"/api/v2/communities/{g.Id}/inventory?search=alpha", TestContext.Current.CancellationToken))!.Items.Length);
    }
}
