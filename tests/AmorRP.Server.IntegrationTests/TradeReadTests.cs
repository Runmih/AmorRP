using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trade_projection_releases_the_group_write_lock_and_reads_one_consistent_snapshot(bool list)
    {
        var probe = new TradeProjectionProbe();
        using var factory = Factory().WithWebHostBuilder(b => b.ConfigureServices(services =>
            services.ConfigureDbContext<AmorDbContext>(options => options.AddInterceptors(probe))));
        await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "720");
        var group = await Create(owner); using var member = factory.CreateClient(); var other = await Login(member, "721");
        await Join(member, await Invite(owner, group));
        var recipe = await Recipe(owner, group);
        var potion = (await Command<ProductionResult>(owner, HttpMethod.Post, $"/api/v1/groups/{group.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 3))).Holding;
        var trade = await Command<Trade>(owner, HttpMethod.Post, $"/api/v1/groups/{group.Id}/trades", new TradeCreate(other.Character.Id));
        trade = await TradeCommand(member, group, trade, "accept");
        trade = await Offer(owner, group, trade, "0", new TradeLine(potion.Id, 1));

        probe.Arm();
        var path = $"/api/v1/groups/{group.Id}/trades" + (list ? "?limit=100" : $"/{trade.Id}");
        var reading = member.GetAsync(path, TestContext.Current.CancellationToken);
        var lockAvailable = false;
        Trade? updated = null;
        try
        {
            await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            await using var connection = new NpgsqlConnection(Connection); await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var tx = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await using var check = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(hashtextextended(@resource, 0))", connection, tx);
            check.Parameters.AddWithValue("resource", "group:" + group.Id);
            lockAvailable = (bool)(await check.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
            await tx.RollbackAsync(TestContext.Current.CancellationToken);
            if (lockAvailable)
                updated = await Offer(owner, group, trade, "0", new TradeLine(potion.Id, 2))
                    .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        }
        finally { probe.Resume(); }
        using var response = await reading.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(lockAvailable, "Trade response projection still holds the group-wide write lock.");
        var snapshot = list ? (await response.Content.ReadFromJsonAsync<TradePage>(TestContext.Current.CancellationToken))!.Items.Single()
            : (await response.Content.ReadFromJsonAsync<Trade>(TestContext.Current.CancellationToken))!;
        Assert.Equal(trade.Revision, snapshot.Revision);
        Assert.Equal(1, snapshot.Initiator.Items.Single().Quantity);
        Assert.NotNull(updated); Assert.Equal(trade.Revision + 1, updated.Revision);
        var current = await ReadTrade(member, group, trade);
        Assert.Equal(updated.Revision, current.Revision); Assert.Equal(2, current.Initiator.Items.Single().Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trade_read_commits_expiry_even_when_the_requested_id_or_cursor_is_invalid(bool list)
    {
        using var factory = Factory(); await Migrate(factory);
        using var owner = factory.CreateClient(); var identity = await Login(owner, "722"); var group = await Create(owner);
        using var member = factory.CreateClient(); var other = await Login(member, "723"); await Join(member, await Invite(owner, group));
        var recipe = await Recipe(owner, group);
        var potion = (await Command<ProductionResult>(owner, HttpMethod.Post, $"/api/v1/groups/{group.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 3))).Holding;
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v1/groups/{group.Id}/currency/adjustments", new CurrencyAdjustment(identity.Character.Id, "10", "Seed"));
        var trade = await Command<Trade>(owner, HttpMethod.Post, $"/api/v1/groups/{group.Id}/trades", new TradeCreate(other.Character.Id));
        trade = await TradeCommand(member, group, trade, "accept");
        trade = await Offer(owner, group, trade, "5", new TradeLine(potion.Id, 2));
        clock.UtcNow = clock.UtcNow.AddMinutes(11);
        var path = $"/api/v1/groups/{group.Id}/trades" + (list ? "?cursor=invalid" : $"/{Guid.NewGuid()}");
        using var response = await member.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(list ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal("expired", (await db.Trades.SingleAsync(TestContext.Current.CancellationToken)).State);
        Assert.Equal(0, (await db.Holdings.SingleAsync(x => x.Id == potion.Id, TestContext.Current.CancellationToken)).Reserved);
        Assert.Equal(0, (await db.Memberships.SingleAsync(x => x.GroupId == group.Id && x.CharacterId == identity.Character.Id, TestContext.Current.CancellationToken)).Reserved);
        Assert.Empty(await db.TradeSlots.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Trade_pages_batch_projection_queries_and_keep_snapshots_authors_and_writing_bodies_sealed()
    {
        var counter = new TradeProjectionCounter();
        using var factory = Factory().WithWebHostBuilder(b => b.ConfigureServices(services =>
            services.ConfigureDbContext<AmorDbContext>(options => options.AddInterceptors(counter))));
        await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "724"); var group = await Create(owner);
        using var member = factory.CreateClient(); var other = await Login(member, "725"); await Join(member, await Invite(owner, group));
        var recipe = await Recipe(owner, group);
        var potion = (await Command<ProductionResult>(owner, HttpMethod.Post, $"/api/v1/groups/{group.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 3))).Holding;
        var books = (await member.GetFromJsonAsync<CategoryPage>($"/api/v1/groups/{group.Id}/categories", TestContext.Current.CancellationToken))!.Items.Single(x => x.Name == "Books");
        const string privateBody = "PRIVATE BODY NOT IN TRADE";
        var letter = (await Command<ProductionResult>(member, HttpMethod.Post, $"/api/v1/groups/{group.Id}/inventory/letters", new LetterCreate(books.Id, "Sealed batch letter", privateBody, "letter"))).Holding;
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var trade = await Command<Trade>(owner, HttpMethod.Post, $"/api/v1/groups/{group.Id}/trades", new TradeCreate(other.Character.Id));
            trade = await TradeCommand(member, group, trade, "accept");
            trade = await Offer(owner, group, trade, "0", new TradeLine(potion.Id, i + 1));
            trade = await Offer(member, group, trade, "0", new TradeLine(letter.Id, 1));
            trade = await TradeCommand(owner, group, trade, "cancel"); ids.Add(trade.Id);
        }
        counter.Start();
        using var response = await member.GetAsync($"/api/v1/groups/{group.Id}/trades?limit=2", TestContext.Current.CancellationToken);
        var page = (await response.Content.ReadFromJsonAsync<TradePage>(TestContext.Current.CancellationToken))!;
        Assert.Equal(4, counter.Stop());
        Assert.Equal(2, page.Items.Length); Assert.NotNull(page.NextCursor);
        Assert.DoesNotContain(privateBody, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        counter.Start();
        var next = (await member.GetFromJsonAsync<TradePage>($"/api/v1/groups/{group.Id}/trades?limit=2&cursor={Uri.EscapeDataString(page.NextCursor!)}", TestContext.Current.CancellationToken))!;
        Assert.Equal(4, counter.Stop());
        Assert.Single(next.Items); Assert.Null(next.NextCursor);
        var all = page.Items.Concat(next.Items).ToArray();
        Assert.Equal(ids.Order(), all.Select(x => x.Id).Order());
        Assert.All(all, trade =>
        {
            Assert.Equal("cancelled", trade.State); Assert.Equal(group.Currency.Id, trade.CurrencyId);
            var item = trade.Initiator.Items.Single(); Assert.Equal(recipe.Id, item.DefinitionId); Assert.Equal(1, item.DefinitionRevision);
            Assert.Equal(ids.IndexOf(trade.Id) + 1, item.Quantity);
            Assert.Equal(other.Character.Id, trade.Recipient.Items.Single().Author!.Id);
            Assert.Equal("Sealed batch letter", trade.Recipient.Items.Single().Name);
        });
    }
}

internal sealed class TradeProjectionProbe : DbCommandInterceptor
{
    private int armed;
    public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Arm() => Interlocked.Exchange(ref armed, 1);
    public void Resume() => resume.TrySetResult();
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FROM groups AS", StringComparison.Ordinal) && Interlocked.CompareExchange(ref armed, 0, 1) == 1)
        {
            Reached.TrySetResult();
            await resume.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}

internal sealed class TradeProjectionCounter : DbCommandInterceptor
{
    private int counting;
    private int queries;
    public void Start() { queries = 0; Interlocked.Exchange(ref counting, 1); }
    public int Stop() { Interlocked.Exchange(ref counting, 0); return queries; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref counting) == 1 && new[] { "groups", "trade_lines", "characters", "holdings" }
            .Any(table => command.CommandText.Contains("FROM " + table + " AS", StringComparison.Ordinal)))
            Interlocked.Increment(ref queries);
        return ValueTask.FromResult(result);
    }
}
