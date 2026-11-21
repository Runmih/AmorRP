using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private TreasuryBalance? treasuryBalance;
    private TreasuryReceiptPage? treasuryRecords;
    private TreasuryReceipt? lastTreasuryReceipt;
    private Guid? treasuryRequestedFor;
    private int treasuryReadVersion;
    private string treasuryAmount = "", treasuryNote = "";

    private void ClearTreasury()
    {
        treasuryReadVersion++; treasuryRequestedFor = null; treasuryBalance = null;
        treasuryRecords = null; lastTreasuryReceipt = null; treasuryAmount = treasuryNote = "";
    }
    private void LoadTreasury(GroupProfile group, bool force = false, bool more = false)
    {
        if (!force && treasuryRequestedFor == group.Id) return;
        var previous = more ? treasuryRecords : null;
        treasuryRequestedFor = group.Id; var version = ++treasuryReadVersion;
        var uri = origin!; var token = session!.AccessToken;
        var recordsAllowed = CanGroupAction(group, GroupCapabilities.TreasuryRecords);
        Start(async ct => {
            var balance = await client.GetAsync<TreasuryBalance>(uri, GroupPath(group) + "/treasury", token, ct);
            var personal = (await client.GetAsync<BalancePage>(uri, $"api/v2/seasons/{group.CommunityId}/balances", token, ct)).Items.Single();
            var records = recordsAllowed ? await client.GetAsync<TreasuryReceiptPage>(uri, GroupPath(group) + "/treasury/records" + (previous?.NextCursor == null ? "" : "?cursor=" + Uri.EscapeDataString(previous.NextCursor)), token, ct) : null;
            return () => {
                if (version != treasuryReadVersion || selectedGroupId != group.Id || community?.Id != group.CommunityId) return;
                if (treasuryBalance == null || balance.Version >= treasuryBalance.Version) treasuryBalance = balance;
                AcceptTreasuryPersonalBalance(personal);
                treasuryRecords = previous == null || records == null ? records : records with { Items = previous.Items.Concat(records.Items).DistinctBy(x => x.Id).ToArray() };
            };
        }, "Loading treasury…");
    }
    private void AcceptTreasuryPersonalBalance(Balance balance)
    {
        if (community?.Currency.Id != balance.CurrencyId || session?.Character.Id != balance.CharacterId || balance.Version < community.MyBalance.Version) return;
        community = community with { MyBalance = balance };
        communities = communities.Select(x => x.Id == community.Id ? community : x).ToArray();
        if (memberSelection == balance.CharacterId) { selectedMemberBalance = balance; balanceFor = balance.CharacterId; }
    }
    private void DrawGroupTreasury(GroupProfile group, bool management = true)
    {
        LoadTreasury(group);
        if (ActionButton("Refresh treasury", mutation: false)) LoadTreasury(group, true);
        Wrapped("Currency: " + community!.Currency.Name);
        if (treasuryBalance == null) { Wrapped("Loading treasury balance…"); return; }
        Wrapped("Group treasury: " + treasuryBalance.Available);
        Wrapped("Your available coins: " + community.MyBalance.Available);
        FieldLabel("Amount"); ImGui.InputText("##TreasuryAmount", ref treasuryAmount, 20, ImGuiInputTextFlags.CharsDecimal);
        FieldLabel("Transfer note (optional)"); LimitedInput("##TreasuryNote", ref treasuryNote, 300, countCodeUnits: true);
        var valid = WholeUnits.TryNormalize(treasuryAmount, out var amount, out var value) && value > 0;
        ImGui.BeginDisabled(!valid);
        if (ActionButton("Deposit your coins")) Mutate("POST", GroupPath(group) + "/treasury/deposits", new TreasuryTransfer(amount, treasuryNote));
        if (management && CanGroupAction(group, GroupCapabilities.TreasuryWithdraw))
        {
            ImGui.SameLine();
            if (ActionButton("Withdraw to your character")) Mutate("POST", GroupPath(group) + "/treasury/withdrawals", new TreasuryTransfer(amount, treasuryNote));
        }
        ImGui.EndDisabled();
        if (lastTreasuryReceipt != null && ImGui.CollapsingHeader("Last transfer receipt")) DrawTreasuryReceipt(lastTreasuryReceipt);
        if (management && CanGroupAction(group, GroupCapabilities.TreasuryRecords) && ImGui.CollapsingHeader("Treasury records", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (treasuryRecords?.Items.Length == 0) Wrapped("No treasury transfers yet.");
            if (treasuryRecords != null) foreach (var entry in treasuryRecords.Items)
            {
                ImGui.PushID(entry.Id.ToString());
                if (ImGui.TreeNode($"{entry.OccurredAt:dd MMM HH:mm} · {TreasuryKind(entry.Kind)} · {entry.Amount} · {entry.CharacterName}"))
                { DrawTreasuryReceipt(entry); ImGui.TreePop(); }
                ImGui.PopID();
            }
            if (treasuryRecords?.NextCursor != null && ActionButton("Older treasury records", mutation: false)) LoadTreasury(group, true, true);
        }
    }
    private static string TreasuryKind(string kind) => kind switch { "deposit" => "Deposit", "withdrawal" => "Withdrawal", "disband_return" => "Disband return", _ => kind };
    private static void DrawTreasuryReceipt(TreasuryReceipt entry)
    {
        Wrapped(TreasuryKind(entry.Kind) + ": " + entry.Amount);
        Wrapped("Character: " + entry.CharacterName);
        Wrapped($"Treasury: {entry.GroupBefore} → {entry.GroupAfter}");
        Wrapped($"Character balance: {entry.CharacterBefore} → {entry.CharacterAfter}");
        if (entry.Note.Length > 0) Wrapped(entry.Note);
        Wrapped("Receipt: " + entry.Id);
    }
    private void ApplyTreasurySuccess(string json)
    {
        var result = ReadCommand<TreasuryTransferResult>(json);
        treasuryReadVersion++; treasuryRequestedFor = null;
        treasuryBalance = result.Treasury; lastTreasuryReceipt = result.Receipt;
        AcceptTreasuryPersonalBalance(result.CharacterBalance);
        if (WholeUnits.TryNormalize(result.Receipt.CharacterAfter, out _, out var beforeRepayment)
            && WholeUnits.TryNormalize(result.CharacterBalance.Owned, out _, out var afterRepayment) && afterRepayment < beforeRepayment)
            actionResult = "Coins received and applied to unpaid bills. See Your bills and debt on Home.";
        treasuryAmount = treasuryNote = "";
    }
}
