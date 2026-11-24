using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    public Action<bool>? SetLauncherVisible { get; set; }
    private int inventoryTab;
    private void DrawInventoryTabs()
    {
        if (community == null) { DrawNoSeason(); return; }
        if (!ImGui.BeginTabBar("InventorySubtabs")) return;
        if (ImGui.BeginTabItem("All")) { inventoryTab = 0; DrawInventory(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Consumables")) { inventoryTab = 1; DrawInventory(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
    private void DrawCraftingTabs()
    {
        if (!CanCraftPotions && !CanCraftCooking && !CanGather)
        {
            Wrapped("Ask the season owner or an authorized moderator for Alchemy or Cooking access.");
            return;
        }
        DrawRefreshState(inventoryRefresh, "Crafting");
        if (!inventoryRefresh.HasLoaded && definitions.Length == 0) return;
        if (!ImGui.BeginTabBar("CraftingSubtabs")) return;
        if (CanCraftPotions && ImGui.BeginTabItem("Alchemy")) { SetCraftMode(false); DrawQuota("potion_points"); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanCraftCooking && ImGui.BeginTabItem("Cooking")) { SetCraftMode(true); DrawQuota("cooking_points"); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanGather && ImGui.BeginTabItem("Gathering and Hunting")) { DrawGathering(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    private void DrawRecipeCreationTabs()
    {
        if (!CanOpenRecipeCreation) return;
        DrawRefreshState(inventoryRefresh, "Recipe Creation");
        if (!inventoryRefresh.HasLoaded && definitions.Length == 0) return;
        if (!ImGui.BeginTabBar("RecipeCreationSubtabs")) return;
        if (CanDefinePotions && ImGui.BeginTabItem("Alchemy recipes")) { SetRecipeMode(false); DrawCatalogEditor(); ImGui.EndTabItem(); }
        if (CanDefineCooking && ImGui.BeginTabItem("Cooking recipes")) { SetRecipeMode(true); DrawCatalogEditor(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
}
