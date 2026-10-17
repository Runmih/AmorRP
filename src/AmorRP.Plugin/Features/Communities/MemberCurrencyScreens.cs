using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Communities;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
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
        var communityId = community!.Id; var characterId = member.Character.Id;
        var uri = origin!; var token = session!.AccessToken;
        balanceRequestedFor = characterId;
        Start(async ct =>
        {
            var page = await client.GetAsync<BalancePage>(uri, $"api/v2/communities/{communityId}/members/{characterId}/balances", token, ct);
            return () =>
            {
                if (community?.Id != communityId || memberSelection != characterId || !CanCoins) return;
                var balance = page.Items.Single();
                if (balanceFor == characterId && selectedMemberBalance?.Version > balance.Version) return;
                selectedMemberBalance = balance; balanceFor = characterId;
                if (characterId == session!.Character.Id && balance.Version >= community.MyBalance.Version)
                {
                    community = community with { MyBalance = balance };
                    communities = communities.Select(x => x.Id == community.Id ? community : x).ToArray();
                }
            };
        }, "Refreshing balance…");
    }

    private void DrawMemberCurrency(Member target)
    {
        if (community == null) return;
        ImGui.TextUnformatted("Balance — " + community.Currency.Name);
        ImGui.SameLine();
        if (ActionButton("Refresh##MemberBalance", mutation: false)) RefreshMemberBalance(target);
        var balance = balanceFor == target.Character.Id ? selectedMemberBalance : target.Character.Id == session!.Character.Id ? community.MyBalance : null;
        if (target.Character.Id == session!.Character.Id && (balance == null || community.MyBalance.Version > balance.Version)) balance = community.MyBalance;
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
            Mutate("POST", $"api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(target.Character.Id, delta, reason));
    }
}
