using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private BillPage myBills = new([], null), groupBills = new([], null);
    private Guid? billingGroupRequested, billingCustomer;
    private int billingReadVersion, billingCustomerReadVersion;
    private Guid billingDraftId = Guid.CreateVersion7();
    private string billingAmount = "", billingService = "", billingSearch = "";
    private MemberPage billingCustomers = new([], null, "0", null);
    private bool billingCustomersLoaded;
    private BillingPolicy? billingPolicy;
    private bool allowDebtDraft, allowDebtSaved;
    private string billingPolicyEtag = "";
    private readonly Dictionary<Guid, BillPaymentPage> billPayments = [];

    private void ClearGroupBilling()
    {
        billingReadVersion++; billingCustomerReadVersion++; billingGroupRequested = billingCustomer = null;
        groupBills = new([], null); billingCustomers = new([], null, "0", null); billingCustomersLoaded = false;
        billingDraftId = Guid.CreateVersion7(); billingAmount = billingService = billingSearch = ""; billPayments.Clear();
    }
    private void ClearBilling()
    {
        ClearGroupBilling(); myBills = new([], null); billingPolicy = null; billingPolicyEtag = "";
        allowDebtDraft = allowDebtSaved = false;
    }
    private void AcceptBillingPolicy(BillingPolicy policy)
    {
        billingPolicy = policy;
        if (billingPolicyEtag.Length == 0 || allowDebtDraft == allowDebtSaved)
        { allowDebtDraft = allowDebtSaved = policy.AllowDebt; billingPolicyEtag = policy.Etag; }
    }
    private void DrawBillingPolicy()
    {
        DrawRefreshState(billingPolicyRefresh, "Billing settings");
        if (billingPolicy == null) return;
        ImGui.Checkbox("Allow unpaid bills to become debt", ref allowDebtDraft);
        Wrapped("When this is off, an unaffordable bill fails without charging coins. Existing debts continue to be repaid.");
        var stale = billingPolicyEtag != billingPolicy.Etag;
        if (stale) { Wrapped("Billing settings changed. Reload before saving."); if (ImGui.Button("Reload billing settings")) { billingPolicyEtag = ""; AcceptBillingPolicy(billingPolicy); } }
        ImGui.BeginDisabled(stale || allowDebtDraft == allowDebtSaved);
        if (ActionButton("Save billing settings")) Mutate("PUT", $"api/v2/communities/{community!.Id}/billing-policy", new BillingPolicyEdit(allowDebtDraft), billingPolicyEtag);
        ImGui.EndDisabled();
    }
    private void DrawTrust(GroupProfile group)
    {
        if (group.EnabledFeatures?.Contains(GroupCapabilities.Treasury) != true && !group.IsTrusted) return;
        var trusted = group.IsTrusted;
        ImGui.BeginDisabled(WriteBlocked);
        if (ImGui.Checkbox("Trusted for automatic billing", ref trusted))
            Mutate("PUT", GroupPath(group) + "/trust", new GroupTrustEdit(trusted), $"\"group-trust:{group.Id:N}:{session!.Character.Id:N}:{group.TrustVersion}\"");
        ImGui.EndDisabled();
        Wrapped("Trusted groups can charge your character for services, including while you are offline. Removing trust stops new bills; existing debt remains payable.");
    }
    private void DrawMyBills()
    {
        DrawRefreshState(myBillsRefresh, "Your bills");
        if (myBills.TotalOutstanding != "0") Wrapped("Debt still owed: " + myBills.TotalOutstanding + " " + community!.Currency.Name);
        if (!ImGui.CollapsingHeader("Your bills and debt")) return;
        Wrapped("Available coins and future income repay your oldest bills automatically.");
        if (community!.MyBalance.HeldForDebt != "0") Wrapped("Coins awaiting automatic repayment: " + community.MyBalance.HeldForDebt);
        if (ActionButton("Refresh your bills", mutation: false)) { myBillsRefresh.Invalidate(); billPayments.Clear(); }
        DrawBills(myBills);
        if (myBills.NextCursor != null && ActionButton("Older bills", mutation: false))
        {
            var id = community.Id; var previous = myBills; var uri = origin!; var token = session!.AccessToken;
            Start(async ct => {
                var page = await client.GetAsync<BillPage>(uri, $"api/v2/communities/{id}/bills/mine?cursor=" + Uri.EscapeDataString(previous.NextCursor), token, ct);
                return () => { if (community?.Id == id) myBills = page with { Items = previous.Items.Concat(page.Items).DistinctBy(x => x.Id).ToArray() }; };
            }, "Loading older bills…");
        }
    }
    private void DrawGroupBilling(GroupProfile group)
    {
        if (billingGroupRequested != group.Id) LoadGroupBills(group);
        if (ActionButton("Refresh bills", mutation: false)) { LoadGroupBills(group); treasuryRequestedFor = null; billPayments.Clear(); }
        if (CanGroupAction(group, GroupCapabilities.Billing)) DrawBillComposer(group);
        Wrapped("Outstanding on these bills: " + groupBills.TotalOutstanding + " " + community!.Currency.Name);
        DrawBills(groupBills);
        if (groupBills.NextCursor != null && ActionButton("Older group bills", mutation: false)) LoadGroupBills(group, true);
    }
    private void DrawBillComposer(GroupProfile group)
    {
        Wrapped("Charge a trusted customer for a service. Bills can be issued while the customer is offline.");
        FieldLabel("Find customer"); LimitedInput("##BillingSearch", ref billingSearch, 100);
        if (!billingCustomersLoaded || ActionButton("Search customers", mutation: false)) LoadBillingCustomers();
        var selected = billingCustomers.Items.FirstOrDefault(x => x.Character.Id == billingCustomer);
        if (ImGui.BeginCombo("Customer", selected == null ? "Choose a community member" : selected.Character.DisplayName + " @ " + selected.Character.HomeWorldName))
        {
            foreach (var member in billingCustomers.Items)
                if (ImGui.Selectable(member.Character.DisplayName + " @ " + member.Character.HomeWorldName + "###billcustomer" + member.Character.Id, billingCustomer == member.Character.Id)) billingCustomer = member.Character.Id;
            ImGui.EndCombo();
        }
        if (billingCustomers.NextCursor != null && ActionButton("More customers", mutation: false)) LoadBillingCustomers(true);
        FieldLabel("Service"); LimitedInput("##BillService", ref billingService, 100);
        FieldLabel("Amount"); ImGui.InputText("##BillAmount", ref billingAmount, 20, ImGuiInputTextFlags.CharsDecimal);
        var valid = billingCustomer != null && WholeUnits.TryNormalize(billingAmount, out _, out var value) && value > 0 && AmorRP.Core.Items.PlainText.Error(billingService, 100) == null;
        ImGui.BeginDisabled(!valid);
        if (ActionButton("Charge customer")) { WholeUnits.TryNormalize(billingAmount, out var amount, out _); Mutate("POST", GroupPath(group) + "/bills", new BillCreate(billingDraftId, billingCustomer!.Value, amount, billingService)); }
        ImGui.EndDisabled(); ImGui.Separator();
    }
    private void LoadGroupBills(GroupProfile group, bool more = false)
    {
        billingGroupRequested = group.Id; var version = ++billingReadVersion; var previous = more ? groupBills : null;
        var uri = origin!; var token = session!.AccessToken;
        Start(async ct => {
            var page = await client.GetAsync<BillPage>(uri, GroupPath(group) + "/bills" + (previous?.NextCursor == null ? "" : "?cursor=" + Uri.EscapeDataString(previous.NextCursor)), token, ct);
            return () => { if (version == billingReadVersion && community?.Id == group.CommunityId && selectedGroupId == group.Id) groupBills = previous == null ? page : page with { Items = previous.Items.Concat(page.Items).DistinctBy(x => x.Id).ToArray() }; };
        }, "Loading bills…");
    }
    private void LoadBillingCustomers(bool more = false)
    {
        billingCustomersLoaded = true; var version = ++billingCustomerReadVersion; var previous = more ? billingCustomers : null;
        var id = community!.Id; var uri = origin!; var token = session!.AccessToken; var groupId = selectedGroupId;
        var query = "?search=" + Uri.EscapeDataString(billingSearch) + (previous?.NextCursor == null ? "" : "&cursor=" + Uri.EscapeDataString(previous.NextCursor));
        Start(async ct => {
            var page = await client.GetAsync<MemberPage>(uri, $"api/v2/communities/{id}/members" + query, token, ct);
            return () => { if (version != billingCustomerReadVersion || community?.Id != id || selectedGroupId != groupId) return; billingCustomers = previous == null ? page : page with { Items = previous.Items.Concat(page.Items).DistinctBy(x => x.Character.Id).ToArray() }; if (!billingCustomers.Items.Any(x => x.Character.Id == billingCustomer)) billingCustomer = null; };
        }, "Finding customers…");
    }
    private void DrawBills(BillPage page)
    {
        if (page.Items.Length == 0) { Wrapped("No bills yet."); return; }
        foreach (var bill in page.Items)
        {
            ImGui.PushID(bill.Id.ToString());
            if (ImGui.TreeNode($"{bill.CreatedAt:dd MMM HH:mm} · {bill.Service} · {bill.Amount}"))
            {
                Wrapped(bill.GroupName + " · " + bill.CharacterName + " · issued by " + bill.IssuerName);
                Wrapped("Paid: " + bill.Collected); Wrapped("Still owed: " + bill.Outstanding);
                Wrapped("Receipt: " + bill.Id);
                if (ActionButton("Payment details", mutation: false)) LoadBillPayments(bill);
                if (billPayments.TryGetValue(bill.Id, out var payments))
                {
                    foreach (var payment in payments.Items) Wrapped($"{payment.OccurredAt:dd MMM HH:mm}: {payment.Amount} paid · balance {payment.CharacterBefore} → {payment.CharacterAfter}");
                    if (payments.NextCursor != null && ActionButton("Older payments", mutation: false)) LoadBillPayments(bill, true);
                }
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
    }
    private void LoadBillPayments(Bill bill, bool more = false)
    {
        var uri = origin!; var token = session!.AccessToken; var previous = more ? billPayments.GetValueOrDefault(bill.Id) : null; var generation = billingReadVersion;
        Start(async ct => {
            var page = await client.GetAsync<BillPaymentPage>(uri, $"api/v2/communities/{bill.CommunityId}/bills/{bill.Id}/payments" + (previous?.NextCursor == null ? "" : "?cursor=" + Uri.EscapeDataString(previous.NextCursor)), token, ct);
            return () => { if (community?.Id != bill.CommunityId || generation != billingReadVersion) return; billPayments[bill.Id] = previous == null ? page : page with { Items = previous.Items.Concat(page.Items).DistinctBy(x => x.Id).ToArray() }; };
        }, "Loading payment details…");
    }
    private async Task<BillPage> LoadMyBillsAsync(Uri uri, string token, Guid id, int count, CancellationToken ct)
    {
        var page = await client.GetAsync<BillPage>(uri, $"api/v2/communities/{id}/bills/mine", token, ct); var rows = page.Items.ToList();
        while (page.NextCursor != null && rows.Count < count) { page = await client.GetAsync<BillPage>(uri, $"api/v2/communities/{id}/bills/mine?cursor=" + Uri.EscapeDataString(page.NextCursor), token, ct); rows.AddRange(page.Items); }
        return page with { Items = rows.ToArray() };
    }
    private void ApplyBillingSuccess(string json)
    {
        var bill = ReadCommand<Bill>(json); billingReadVersion++; billingGroupRequested = null; treasuryRequestedFor = null;
        billingDraftId = Guid.CreateVersion7(); billingAmount = billingService = ""; myBillsRefresh.Invalidate(); billPayments.Clear();
        actionResult = $"Bill recorded: {bill.Collected} paid; {bill.Outstanding} still owed.";
    }
}
