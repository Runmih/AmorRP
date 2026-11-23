using AmorRP.Plugin.Features.Inventory;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private void DrawCharacterPage()
    {
        DrawCharacterClasses();
        ImGui.Spacing();
        DrawCharacterResources();
        ImGui.Spacing();
        DrawCharacterDescription();
        ImGui.Spacing();
        DrawMyBills();
    }

    public Action<bool>? SetCoinHudVisible { get; set; }

    public string[] CoinBalance() => community is { } current && session != null && myBillsRefresh.HasLoaded
        ? [InventoryCompanionQuery.FormatNetBalance(current.MyBalance.Available, myBills.TotalOutstanding) + " " + current.Currency.Name]
        : [];

    public void DrawCoinHud(string[] balance)
    {
        if (configuration.CoinHud.ShowHeader) ImGui.TextUnformatted("Coins");
        if (balance.Length == 0) return;
        UiIcons.Draw(FontAwesomeIcon.Coins);
        ImGui.SameLine();
        ImGui.TextColored(InventoryGold, balance[0]);
    }

    private void DrawCharacterResources()
    {
        DrawSectionHeading("Your resources");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, PluginTheme.SelectedButton);
        var alchemyReset = CanCraftPotions ? DrawResourceBar("potion_points", "Alchemy points", FontAwesomeIcon.Flask) : null;
        var cookingReset = CanCraftCooking ? DrawResourceBar("cooking_points", "Cooking points", FontAwesomeIcon.KitchenSet) : null;
        var gatheringReset = CanGather ? DrawResourceBar("gathering_points", "Gathering and Hunting points", FontAwesomeIcon.Leaf) : null;
        var writingReset = DrawResourceBar("letters", "Writing allowance", FontAwesomeIcon.FeatherAlt);
        ImGui.PopStyleColor();
        if ((alchemyReset ?? cookingReset ?? gatheringReset ?? writingReset) is { } reset)
            ImGui.TextDisabled($"Next reset: {reset.LocalDateTime:g}");
    }

    private DateTimeOffset? DrawResourceBar(string kind, string label, FontAwesomeIcon icon)
    {
        var quota = quotas?.Items.FirstOrDefault(item => item.Kind == kind);
        if (quota == null) return null;
        UiIcons.Draw(icon);
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        var fraction = quota.Limit > 0 ? Math.Clamp((float)quota.Remaining / quota.Limit, 0, 1) : quota.Remaining > 0 ? 1 : 0;
        ImGui.ProgressBar(fraction, new(-1, 0), $"{quota.Remaining:N0} / {quota.Limit:N0}");
        return quota.ResetAt;
    }
}
