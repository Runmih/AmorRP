using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Discord;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Permits;
using AmorRP.Contracts.Shop;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Group_storage_preserves_item_revisions_and_returns_stock_to_disbanded_group_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21601");
        var season = await Create(owner);
        var group = await TreasuryGroup(owner, season);
        var path = TreasuryPath(season, group)[..^9];
        var definition = await Recipe(owner, season);
        var inventory = $"/api/v2/seasons/{season.Id}/inventory";
        var holding = (await Command<ProductionResult>(owner, HttpMethod.Post, inventory + "/potions",
            new PotionCreate(definition.Id, definition.Revision, 3))).Holding;
        await Command<Guid>(owner, HttpMethod.Post, path + "/storage", new GroupStockMove(holding.Id, holding.Version, 3, false));
        Assert.Empty((await owner.GetFromJsonAsync<HoldingPage>(inventory, ct))!.Items);
        var stock = Assert.Single((await owner.GetFromJsonAsync<GroupStorage>(path + "/storage", ct))!.Items);
        Assert.Equal(3, stock.Quantity);
        using var staff = factory.CreateClient();
        var staffId = (await Login(staff, "21602")).Character.Id;
        await GroupAction(staff, HttpMethod.Post, path + "/join", new GroupJoin());
        await GrantStaff(owner, path, staffId, GroupCapabilities.Storage);
        var withdraw = new GroupStockMove(stock.Id, stock.Version, 1, true);
        using (var request = Request(HttpMethod.Post, path + "/storage", withdraw))
        using (var denied = await staff.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantStaff(owner, path, staffId, GroupCapabilities.StorageWithdraw);
        await Command<Guid>(staff, HttpMethod.Post, path + "/storage", withdraw);
        var received = Assert.Single((await staff.GetFromJsonAsync<HoldingPage>(inventory, ct))!.Items);
        Assert.Equal(definition.Revision, received.DefinitionRevision);
        var current = (await owner.GetFromJsonAsync<GroupProfile>(path, ct))!;
        await Command<GroupProfile>(owner, HttpMethod.Delete, path, etag: current.Etag);
        Assert.Equal(2, Assert.Single((await owner.GetFromJsonAsync<HoldingPage>(inventory, ct))!.Items).Quantity);
    }

    [Fact]
    public async Task Staff_schedule_and_individual_commission_preserve_existing_clock_in_and_billing_rules()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21610");
        var season = await Create(owner);
        var group = await TreasuryGroup(owner, season);
        var path = TreasuryPath(season, group)[..^9];
        using var staff = factory.CreateClient();
        var staffId = (await Login(staff, "21611")).Character.Id;
        await GroupAction(staff, HttpMethod.Post, path + "/join", new GroupJoin());
        var member = await StaffRow(owner, path, staffId);
        await Command<GroupMember>(owner, HttpMethod.Put, path + "/members/" + staffId + "/capabilities",
            new GroupStaffEdit([GroupCapabilities.Billing], 25, true), member.Etag);
        var shift = new GroupShiftCreate(staffId, clock.UtcNow.AddHours(1), clock.UtcNow.AddHours(2));
        var shiftId = await Command<Guid>(owner, HttpMethod.Post, path + "/shifts", shift);
        Assert.Equal(shiftId, Assert.Single((await staff.GetFromJsonAsync<GroupShifts>(path + "/shifts", ct))!.Items).Id);
        using (var request = Request(HttpMethod.Post, path + "/shifts", shift))
        using (var conflict = await owner.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using (var request = Request(HttpMethod.Delete, path + "/shifts/" + shiftId))
        using (var denied = await staff.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var customer = factory.CreateClient();
        var customerId = (await Login(customer, "21612")).Character.Id;
        await Fund(owner, season, customerId, "40");
        await TrustBilling(customer, season, group);
        var bill = await Charge(staff, season, group, customerId, "40");
        Assert.Equal(25, bill.CommissionPercent);
        Assert.Equal("10", (await OwnBalance(staff, season)).Owned);
        await Command<Guid>(owner, HttpMethod.Delete, path + "/shifts/" + shiftId);
        Assert.Empty((await staff.GetFromJsonAsync<GroupShifts>(path + "/shifts", ct))!.Items);
    }

    [Fact]
    public async Task Permit_transfer_moves_the_registration_and_billing_lock_once_after_consent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var seller = factory.CreateClient();
        var sellerId = (await Login(seller, "21620")).Character.Id;
        var season = await Create(seller);
        var sellerGroup = await TreasuryGroup(seller, season);
        using var buyer = factory.CreateClient();
        var buyerId = (await Login(buyer, "21621")).Character.Id;
        var seasonPath = $"/api/v2/seasons/{season.Id}";
        await GrantHome(seller, season, buyerId, "groups.create");
        var buyerGroup = await Command<GroupProfile>(buyer, HttpMethod.Post, seasonPath + "/groups", new GroupWrite("Receiving group", ""));
        await Command<GroupProfile>(seller, HttpMethod.Put, seasonPath + $"/groups/{buyerGroup.Id}/features",
            new GroupFeaturesEdit([GroupCapabilities.Treasury]), buyerGroup.Etag);
        await Fund(seller, season, sellerId, "20");
        await TreasuryMove(seller, season, sellerGroup, "20");
        await Fund(seller, season, buyerId, "50");
        await TreasuryMove(buyer, season, buyerGroup, "50");
        using var subject = factory.CreateClient();
        var subjectId = (await Login(subject, "21622")).Character.Id;
        var permits = seasonPath + "/permits";
        await Command<PermitPolicy>(seller, HttpMethod.Put, permits + "/policy", new PermitPolicy("20", true));
        var offer = await Command<PermitOffer>(seller, HttpMethod.Post, permits + "/offers", new PermitOfferCreate(subjectId, sellerGroup.Id, "20"));
        var quote = Assert.Single((await subject.GetFromJsonAsync<PermitShop>(permits, ct))!.Offers);
        var registered = await Command<OwnershipPermit>(subject, HttpMethod.Put, permits + $"/offers/{offer.Id}/accept", new PermitQuoteAction(quote.Quote));
        var transfer = await Command<Guid>(seller, HttpMethod.Post, permits + $"/{registered.Id}/transfers", new PermitTransferCreate(buyerGroup.Id, "15"));
        var transferPath = seasonPath + "/permit-transfers/" + transfer;
        using (var request = Request(HttpMethod.Put, transferPath + "/acceptance"))
        using (var denied = await buyer.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Command<Guid>(subject, HttpMethod.Put, transferPath + "/approval");
        await Command<Guid>(buyer, HttpMethod.Put, transferPath + "/acceptance");
        Assert.False((await subject.GetFromJsonAsync<GroupProfile>(TreasuryPath(season, sellerGroup)[..^9], ct))!.BillingTrustLocked);
        Assert.True((await subject.GetFromJsonAsync<GroupProfile>(TreasuryPath(season, buyerGroup)[..^9], ct))!.BillingTrustLocked);
        Assert.Equal(buyerGroup.Id, Assert.Single((await subject.GetFromJsonAsync<PermitShop>(permits, ct))!.Permits).GroupId);
        Assert.Equal("35", (await buyer.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(season, buyerGroup), ct))!.Owned);
        Assert.Equal("15", (await seller.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(season, sellerGroup), ct))!.Owned);
    }

    [Fact]
    public async Task Discord_opt_in_channel_policy_and_message_replay_bound_coin_awards()
    {
        var ct = TestContext.Current.CancellationToken;
        const string botKey = "disposable-integration-test-service-key";
        using var factory = Factory().WithWebHostBuilder(builder => builder.UseSetting("DiscordRewards:ServiceKey", botKey));
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21630");
        var season = await Create(owner);
        var group = await TreasuryGroup(owner, season);
        var groupPath = TreasuryPath(season, group)[..^9];
        var config = $"/api/v2/seasons/{season.Id}/discord";
        await Command<DiscordRewardPolicy>(owner, HttpMethod.Put, config + "/policy",
            new DiscordRewardPolicy(true, 10, 20, 10, 15, 30, 100));

        using var bot = factory.CreateClient();
        using (var unauthenticated = await bot.GetAsync("/api/v2/integrations/discord/guilds", ct))
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        bot.DefaultRequestHeaders.Add("X-AmorRP-Discord-Key", botKey);
        const string guild = "1234567890123456";
        const string admin = "1234567890123457";
        const string writer = "1234567890123458";
        const string channel = "1234567890123459";
        const string root = "/api/v2/integrations/discord";

        var groupCode = await Command<DiscordCode>(owner, HttpMethod.Post, groupPath + "/discord/link");
        using (var connected = await bot.PostAsJsonAsync(root + "/guilds/redeem", new DiscordRedeem(groupCode.Code, guild, admin), ct))
            Assert.Equal(HttpStatusCode.OK, connected.StatusCode);
        using (var configured = await bot.PutAsJsonAsync(root + "/guilds/" + guild + "/channels", new DiscordChannels([channel], null, false), ct))
            Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
        using var player = factory.CreateClient();
        await Login(player, "21631");
        var post = new DiscordPost("1234567890123460", guild, channel, writer, [], 100, new string('a', 64), clock.UtcNow);
        async Task<int> Award(DiscordPost message)
        {
            using var result = await bot.PostAsJsonAsync(root + "/posts", message, ct);
            Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync(ct));
            return (await result.Content.ReadFromJsonAsync<DiscordAward>(ct))!.Amount;
        }
        Assert.Equal(0, await Award(post));
        var playerCode = await Command<DiscordCode>(player, HttpMethod.Post, config + "/link");
        using (var linked = await bot.PostAsJsonAsync(root + "/players/redeem", new DiscordRedeem(playerCode.Code, guild, writer), ct))
            Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.True((await player.GetFromJsonAsync<DiscordPlayerState>(config, ct))!.Linked);
        Assert.Equal(0, await Award(post with { ChannelId = "1234567890123461" }));
        Assert.Equal(10, await Award(post));
        Assert.Equal(0, await Award(post));
        clock.UtcNow = clock.UtcNow.AddSeconds(20);
        Assert.Equal(5, await Award(post with { MessageId = "1234567890123462", Fingerprint = new string('b', 64), CreatedAt = clock.UtcNow }));
        Assert.Equal("15", (await OwnBalance(player, season)).Owned);
        await Command<bool>(player, HttpMethod.Delete, config + "/link");
        Assert.False((await player.GetFromJsonAsync<DiscordPlayerState>(config, ct))!.Linked);
        await Command<bool>(owner, HttpMethod.Delete, groupPath + "/discord/link");
        Assert.Equal(guild, Assert.Single((await bot.GetFromJsonAsync<DiscordGuilds>(root + "/guilds", ct))!.Leave));
    }
}
