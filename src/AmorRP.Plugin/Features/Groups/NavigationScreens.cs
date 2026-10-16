using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    public Action<bool>? SetLauncherVisible { get; set; }
    private int inventoryTab;
    private void DrawHome()
    {
        if (group == null) { Wrapped("Choose a group, or use Join / Create group above."); return; }
        DrawHomeOverview();
        DrawOwnershipOffer();
        if (Owner) { DrawWeeklyPolicy(); DrawGroupSettings(); }
    }
    private void DrawInventoryTabs()
    {
        if (group == null) { Wrapped("Use Join or Create group above to get started."); return; }
        if (!ImGui.BeginTabBar("InventorySubtabs")) return;
        if (ImGui.BeginTabItem("All")) { inventoryTab = 0; DrawInventory(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Consumables")) { inventoryTab = 1; DrawInventory(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Books")) { inventoryTab = 2; DrawInventory(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
    private void DrawCraftingTabs()
    {
        if (group == null) { Wrapped("Use Join or Create group above to get started."); return; }
        if (!ImGui.BeginTabBar("CraftingSubtabs")) return;
        if (ImGui.BeginTabItem("Info"))
        {
            Wrapped("Crafting creates RP items for the selected group.");
            Wrapped("Potions require the Craft Potions permission. Ask the owner or a moderator with Manage Gameplay Permissions for access. Each potion spends your character's weekly potion points.");
            Wrapped("Books, letters and notes are available to all group members within the weekly written-item allowance.");
            Wrapped("Potion recipes define item descriptions, messages and costs. Editing recipes requires the separate Potion Creation moderation permission.");
            ImGui.EndTabItem();
        }
        if (CanCraftPotions && ImGui.BeginTabItem("Potions")) { DrawQuota("potion_points"); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanDefinePotions && ImGui.BeginTabItem("Potion recipes")) { DrawCatalogEditor(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Books")) { DrawQuota("letters"); DrawLetterCreation(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
    private void DrawSettings()
    {
        if (!ImGui.BeginTabBar("SettingsSubtabs")) return;
        if (ImGui.BeginTabItem("General"))
        {
            var show = configuration.ShowLauncher;
            if (ImGui.Checkbox("Show quick launcher", ref show)) { configuration.ShowLauncher = show; save(); SetLauncherVisible?.Invoke(show); }
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }
}
