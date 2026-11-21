using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Bill_receipts_preserve_the_tax_and_cashier_split_after_clock_out_and_rate_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory();
        await Migrate(f);
        using var owner = f.CreateClient();
        await Login(owner, "1400");
        var season = await Create(owner);
        var group = await TreasuryGroup(owner, season);
        var groupPath = TreasuryPath(season, group)[..^9];
        var policyPath = $"/api/v2/seasons/{season.Id}/billing-policy";
        var settingsPath = groupPath + "/billing-settings";
        var clockPath = groupPath + "/clock-in";

        using var staff = f.CreateClient();
        var staffId = (await Login(staff, "1401")).Character.Id;
        await Enroll(staff, season);
        await GroupAction(staff, HttpMethod.Post, groupPath + "/join", new GroupJoin());
        await GrantStaff(owner, groupPath, staffId, GroupCapabilities.Billing);
        using var payer = f.CreateClient();
        var payerId = (await Login(payer, "1402")).Character.Id;
        await Enroll(payer, season);
        await TrustBilling(payer, season, group);
        await Fund(owner, season, payerId, "41");

        var policy = (await owner.GetFromJsonAsync<BillingPolicy>(policyPath, ct))!;
        policy = await Command<BillingPolicy>(owner, HttpMethod.Put, policyPath, new BillingPolicyEdit(true, 17), policy.Etag);
        var settings = (await owner.GetFromJsonAsync<GroupBillingSettings>(settingsPath, ct))!;
        settings = await Command<GroupBillingSettings>(owner, HttpMethod.Put, settingsPath, new GroupBillingEdit(33, true), settings.Etag);

        using (var req = Request(HttpMethod.Post, BillPath(season, group), new BillCreate(Guid.CreateVersion7(), payerId, "103", "Room")))
        using (var denied = await staff.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var clocked = await Command<GroupBillingSettings>(staff, HttpMethod.Put, clockPath, new ClockInEdit(true));
        Assert.NotNull(clocked.ClockedInAt);

        var bill = await Charge(staff, season, group, payerId, "103", "Room");
        Assert.Equal("41", bill.Collected);
        Assert.Equal("62", bill.Outstanding);
        Assert.Equal(17, bill.TaxPercent);
        Assert.Equal(33, bill.CommissionPercent);
        Assert.Equal(new BillAllocation("6", "11", "24"), bill.Allocation);

        await Command<GroupBillingSettings>(owner, HttpMethod.Put, clockPath, new ClockInEdit(false, staffId));
        Assert.Null((await staff.GetFromJsonAsync<GroupBillingSettings>(settingsPath, ct))!.ClockedInAt);
        await Command<BillingPolicy>(owner, HttpMethod.Put, policyPath, new BillingPolicyEdit(false, 0), policy.Etag);
        await Command<GroupBillingSettings>(owner, HttpMethod.Put, settingsPath, new GroupBillingEdit(0, false), settings.Etag);
        await GroupAction(staff, HttpMethod.Delete, groupPath + "/membership");

        await Fund(owner, season, payerId, "62");
        var receipts = (await payer.GetFromJsonAsync<BillPaymentPage>($"/api/v2/seasons/{season.Id}/bills/{bill.Id}/payments", ct))!;
        Assert.Equal(2, receipts.Items.Length);
        Assert.Contains(receipts.Items, x => x.Allocation == new BillAllocation("6", "11", "24"));
        Assert.Contains(receipts.Items, x => x.Allocation == new BillAllocation("11", "17", "34"));
        Assert.Equal("17", (await owner.GetFromJsonAsync<BillingPolicy>(policyPath, ct))!.SeasonTreasury);
        Assert.Null((await payer.GetFromJsonAsync<BillingPolicy>(policyPath, ct))!.SeasonTreasury);
        Assert.Equal("58", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(season, group), ct))!.Owned);
        Assert.Equal("28", (await OwnBalance(staff, season)).Owned);
        Assert.Equal("0", (await OwnBalance(payer, season)).Owned);
    }
}
