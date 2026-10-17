using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    public Action<bool>? SetLauncherVisible { get; set; }
    private int inventoryTab;
    private void DrawHome()
    {
        if (community == null) { Wrapped("Choose a community, or use Join / Create community above."); return; }
        DrawHomeOverview();
        DrawOwnershipOffer();
        if (Owner) { DrawWeeklyPolicy(); DrawCommunitySettings(); }
    }
    private void DrawInventoryTabs()
    {
        if (community == null) { Wrapped("Use Join or Create community above to get started."); return; }
        if (!ImGui.BeginTabBar("InventorySubtabs")) return;
        if (ImGui.BeginTabItem("All")) { inventoryTab = 0; DrawInventory(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Consumables")) { inventoryTab = 1; DrawInventory(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Books")) { inventoryTab = 2; DrawInventory(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
    private void DrawCraftingTabs()
    {
        if (community == null) { Wrapped("Use Join or Create community above to get started."); return; }
        if (!ImGui.BeginTabBar("CraftingSubtabs")) return;
        if (ImGui.BeginTabItem("Info"))
        {
            Wrapped("Crafting creates RP items for the selected community.");
            Wrapped("Alchemy and Cooking turn recipes into usable RP items. Ask the owner or a moderator with Manage Gameplay Permissions for crafting access. Each craft spends your character’s weekly points for that craft.");
            Wrapped("Books, letters and notes are available to all community members within your weekly writing limit. Each book, letter or note counts as one item.");
            Wrapped("Alchemy and Cooking recipes define item descriptions, use messages and point costs. Defining recipes requires the corresponding separate moderation permission.");
            ImGui.EndTabItem();
        }
        if (CanCraftPotions && ImGui.BeginTabItem("Alchemy")) { SetCraftMode(false); DrawQuota("potion_points"); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanDefinePotions && ImGui.BeginTabItem("Alchemy recipes")) { SetCraftMode(false); DrawCatalogEditor(); ImGui.EndTabItem(); }
        if (CanCraftCooking && ImGui.BeginTabItem("Cooking")) { SetCraftMode(true); DrawQuota("cooking_points"); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanDefineCooking && ImGui.BeginTabItem("Cooking recipes")) { SetCraftMode(true); DrawCatalogEditor(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Writing")) { DrawQuota("letters"); DrawLetterCreation(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
    private void DrawSettings()
    {
        if (!ImGui.BeginTabBar("SettingsSubtabs")) return;
        if (ImGui.BeginTabItem("General"))
        {
            var show = configuration.ShowLauncher;
            if (ImGui.Checkbox("Show quick launcher", ref show)) { configuration.ShowLauncher = show; save(); SetLauncherVisible?.Invoke(show); }
            var events = configuration.ShowFavoriteEvents;
            if (ImGui.Checkbox("Show favorite group events", ref events)) { configuration.ShowFavoriteEvents = events; save(); SetFavoriteEventsVisible?.Invoke(events); }
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }
}
