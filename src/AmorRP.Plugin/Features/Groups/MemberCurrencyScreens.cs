using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private Guid? balanceRequestedFor;
    private void ClearMemberBalance()
    { selectedMemberBalance = null; balanceFor = balanceRequestedFor = null; }

    private void EnsureMemberBalance(Member member)
    {
        if (balanceRequestedFor != member.Character.Id) RefreshMemberBalance(member);
    }

    private void RefreshMemberBalance(Member member)
    {
        var groupId = group!.Id; var characterId = member.Character.Id;
        var uri = origin!; var token = session!.AccessToken;
        balanceRequestedFor = characterId;
        Start(async ct =>
        {
            var page = await client.GetAsync<BalancePage>(uri, $"api/v1/groups/{groupId}/members/{characterId}/balances", token, ct);
            return () =>
            {
                if (group?.Id != groupId || memberSelection != characterId || !CanCoins) return;
                var balance = page.Items.Single();
                if (balanceFor == characterId && selectedMemberBalance?.Version > balance.Version) return;
                selectedMemberBalance = balance; balanceFor = characterId;
                if (characterId == session!.Character.Id && balance.Version >= group.MyBalance.Version)
                {
                    group = group with { MyBalance = balance };
                    groups = groups.Select(x => x.Id == group.Id ? group : x).ToArray();
                }
            };
        }, "Refreshing balance…");
    }

    private void DrawMemberCurrency(Member target)
    {
        if (group == null) return;
        ImGui.TextUnformatted("Balance — " + group.Currency.Name);
        ImGui.SameLine();
        if (ActionButton("Refresh##MemberBalance", mutation: false)) RefreshMemberBalance(target);
        var balance = balanceFor == target.Character.Id ? selectedMemberBalance : target.Character.Id == session!.Character.Id ? group.MyBalance : null;
        if (target.Character.Id == session!.Character.Id && (balance == null || group.MyBalance.Version > balance.Version)) balance = group.MyBalance;
        if (balance != null)
        {
            ImGui.TextUnformatted("Total: " + balance.Owned);
            ImGui.TextUnformatted("Available: " + balance.Available);
            ImGui.TextUnformatted("Offered in trades: " + balance.Reserved);
        }
        else ImGui.TextDisabled("Loading balance…");
        ImGui.Separator();
        FieldLabel("Amount to add or remove (e.g. 100 or -25)");
        ImGui.InputText("##MemberCurrencyAmount", ref delta, 32);
        FieldLabel("Reason (required)");
        LimitedInput("##MemberCurrencyReason", ref reason, 500, countCodeUnits: true);
        if (ActionButton("Apply currency adjustment"))
            Mutate("POST", $"api/v1/groups/{group.Id}/currency/adjustments", new CurrencyAdjustment(target.Character.Id, delta, reason));
    }
}
