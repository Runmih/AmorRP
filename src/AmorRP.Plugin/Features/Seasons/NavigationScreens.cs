using AmorRP.Core.Combat;
using AmorRP.Core.Items;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    public Action<bool>? SetLauncherVisible { get; set; }
    private int inventoryTab;
    private bool CanCraft(CraftingDiscipline discipline) => discipline switch
    {
        CraftingDiscipline.Cooking => CanCraftCooking,
        CraftingDiscipline.Blacksmithing => CanBlacksmith,
        _ => CanCraftPotions
    };
    private bool CanDefine(CraftingDiscipline discipline) => discipline switch
    {
        CraftingDiscipline.Cooking => CanDefineCooking,
        CraftingDiscipline.Blacksmithing => CanDefineBlacksmith,
        _ => CanDefinePotions
    };
    private bool CanBlacksmith => Owner || community?.MyCapabilities.Contains(CraftingTypes.BlacksmithPermission) == true;
    private bool CanDefineBlacksmith => Owner || community?.MyCapabilities.Contains(CraftingTypes.BlacksmithRecipePermission) == true;
    private void DrawInventoryTabs()
    {
        if (community == null) { DrawNoSeason(); return; }
        if (!ImGui.BeginTabBar("InventorySubtabs")) return;
        if (ImGui.BeginTabItem("All")) { inventoryTab = 0; DrawInventory(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Consumables")) { inventoryTab = 1; DrawInventory(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Equipment")) { inventoryTab = 3; DrawInventory(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
    private void DrawCraftingTabs()
    {
        if (!CanCraftPotions && !CanCraftCooking && !CanBlacksmith && !CanGather)
        {
            Wrapped("Ask the season owner or an authorized moderator for Alchemy or Cooking access.");
            return;
        }
        DrawRefreshState(inventoryRefresh, "Crafting");
        if (!inventoryRefresh.HasLoaded && definitions.Length == 0) return;
        if (!ImGui.BeginTabBar("CraftingSubtabs")) return;
        if (CanCraftPotions && ImGui.BeginTabItem("Alchemy")) { SetCraftMode(CraftingDiscipline.Alchemy); DrawQuota("potion_points"); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanCraftCooking && ImGui.BeginTabItem("Cooking")) { SetCraftMode(CraftingDiscipline.Cooking); DrawQuota("cooking_points"); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanBlacksmith && ImGui.BeginTabItem("Blacksmithing")) { SetCraftMode(CraftingDiscipline.Blacksmithing); DrawQuota(CombatRules.BlacksmithingQuota); DrawPotionCreation(); ImGui.EndTabItem(); }
        if (CanGather && ImGui.BeginTabItem("Gathering and Hunting")) { DrawGathering(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    private void DrawRecipeCreationTabs()
    {
        if (!CanOpenRecipeCreation) return;
        DrawRefreshState(inventoryRefresh, "Recipe Creation");
        if (!inventoryRefresh.HasLoaded && definitions.Length == 0) return;
        if (!ImGui.BeginTabBar("RecipeCreationSubtabs")) return;
        if (CanDefinePotions && ImGui.BeginTabItem("Alchemy recipes")) { SetRecipeMode(CraftingDiscipline.Alchemy); DrawCatalogEditor(); ImGui.EndTabItem(); }
        if (CanDefineCooking && ImGui.BeginTabItem("Cooking recipes")) { SetRecipeMode(CraftingDiscipline.Cooking); DrawCatalogEditor(); ImGui.EndTabItem(); }
        if (CanDefineBlacksmith && ImGui.BeginTabItem("Blacksmithing recipes")) { SetRecipeMode(CraftingDiscipline.Blacksmithing); DrawCatalogEditor(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
}
