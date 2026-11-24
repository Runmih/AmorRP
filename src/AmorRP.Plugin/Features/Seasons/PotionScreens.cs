using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Plugin.Features.Crafting;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private int recipeChoice, productionQuantity = 1, destination = -1;
    private string productionSearch = "";
    private readonly CraftingRecipeSelection productionSelection = new();
    private DateTimeOffset lastPost;
    private void DrawConsumables()
    {
        if (community == null) { DrawNoSeason(); return; }
        var shown = compact;
        foreach (var item in shown)
        {
            ImGui.PushID(item.Id.ToString());
            if (ImGui.SmallButton(pinned.Contains(item.Id) ? "Unpin" : "Pin")) TogglePin(item.Id);
            ImGui.SameLine(); if (ImGui.Selectable($"{item.Name} ×{item.Available}", holdingSelection == item.Id)) ReadHolding(item);
            ImGui.PopID();
        }
        if (shown.Length == 0) Wrapped("You have no consumables. Craft them or receive them in a trade.");
        LoadMoreInventory(true);
        DrawDiscardDialog(details?.Holding, DrawDetails(details));
    }
    private CraftingDiscipline craftDiscipline;

    private void SetCraftMode(CraftingDiscipline discipline)
    {
        if (craftDiscipline == discipline) return;
        craftDiscipline = discipline;
        recipeChoice = 0;
        productionSearch = "";
        productionSelection.Reset();
    }

    private void DrawPotionCreation()
    {
        if (!CanCraft(craftDiscipline))
        {
            Wrapped("Ask the season owner or a moderator for permission to craft these items.");
            return;
        }
        if (CraftingRecipeSelection.View(definitions, craftDiscipline, "").Length == 0)
        {
            Wrapped(CanDefine(craftDiscipline) ? "No enabled recipes. Define one in Recipe Creation, then return here to craft it." : "No enabled recipes yet. Ask a recipe creator to define and enable one.");
            return;
        }
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 38;
        if (!ImGui.BeginTable("ProductionLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Recipes", ImGuiTableColumnFlags.WidthStretch, .4f); if (!narrow) ImGui.TableSetupColumn("Create", ImGuiTableColumnFlags.WidthStretch, .6f);
        ImGui.TableNextColumn(); ImGui.BeginChild("ProductionRecipes", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 7 : 0), true);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##ProductionSearch", "Search recipes…", ref productionSearch, 100);
        var recipes = CraftingRecipeSelection.View(definitions, craftDiscipline, productionSearch);
        recipeChoice = productionSelection.Resolve(recipes);
        if (ImGui.BeginTable("ProductionRecipeRows", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2);
            ImGui.TableSetupColumn("Cost", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Access", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableHeadersRow();
            for (var i = 0; i < recipes.Length; i++)
            {
                var row = recipes[i];
                ImGui.PushID(row.Id.ToString());
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable(row.Name + "###ProductionRecipe", recipeChoice == i, ImGuiSelectableFlags.SpanAllColumns))
                {
                    recipeChoice = i;
                    productionSelection.Select(row.Id);
                }
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{row.Potion.CreationCost} pts");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(RecipeAccessLabel(row.GroupId));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("ProductionDetails", new(0, 0), true); ImGui.PushItemWidth(-1);
        if (recipes.Length == 0)
        { Wrapped("No recipes match this search. Clear it to see all available recipes."); ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable(); return; }
        var recipe = recipes[recipeChoice]; ImGui.TextUnformatted(recipe.Name); Wrapped(recipe.Description);
        ImGui.Separator();
        if (recipe.Gear is { } gear) ImGui.TextUnformatted(GearLabel(gear));
        else
        {
            ImGui.TextUnformatted("Message posted when used");
            ImGui.BeginChild("ProductionMessage", new(0, ImGui.GetTextLineHeightWithSpacing() * 4), true);
            Wrapped(recipe.Potion.UseMessage);
            ImGui.EndChild();
            if (recipe.StatPoints > 0) ImGui.TextUnformatted($"Grants {recipe.StatPoints} permanent stat points.");
        }
        FieldLabel("Units to craft"); ImGui.InputInt("##ProductionQuantity", ref productionQuantity);
        var cost = (long)productionQuantity * recipe.Potion.CreationCost;
        var remaining = quotas?.Items.FirstOrDefault(x => x.Kind == CraftingTypes.QuotaKind(recipe.TypeId))?.Remaining ?? 0;
        var max = serviceLimits?.MaxCreationQuantity ?? 100;
        var error = productionQuantity < 1 || productionQuantity > max ? $"Choose between 1 and {max} copies."
            : cost > remaining ? $"Not enough points: {cost} needed, {remaining} remaining." : null;
        if (error != null) FieldNote("ProductionQuantity", error, "");
        ImGui.BeginDisabled(error != null);
        if (ActionButton("Craft items##produceRecipeUnits")) Mutate("POST", $"api/v2/seasons/{community!.Id}/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, productionQuantity));
        ImGui.EndDisabled();
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
    private static string DestinationId(int index) => index switch { 0 => "emote", 1 => "say", 2 => "party", >= 3 and <= 10 => "linkshell:" + (index - 2), _ => "crossworld-linkshell:" + (index - 10) };
    private static int DestinationIndex(string id) => Enumerable.Range(0, ChatCommand.Destinations.Length).FirstOrDefault(i => DestinationId(i) == id, -1);
    private bool CanPost(string message, int channel) => channel >= 0 && ChatDestination.Available(player, party, channel)
        && ChatCommand.TryCreate(message, channel, out _, ChatLimit) && DateTimeOffset.UtcNow - lastPost >= TimeSpan.FromSeconds(3);

    private void DrawConsumeButton(HoldingDetails item)
    {
        var sendEmote = destination >= 0;
        ImGui.BeginDisabled(item.Holding.Available < 1 || (sendEmote && !CanPost(item.Definition!.Potion.UseMessage, destination)));
        if (ActionButton("Consume"))
            Mutate("POST", $"api/v2/seasons/{community!.Id}/inventory/{item.Holding.Id}/use",
                new ConsumptionRequest(item.Holding.Version, sendEmote, sendEmote ? DestinationId(destination) : ""));
        ImGui.EndDisabled();
    }

    private void DrawConsumptionDetails(Definition definition)
    {
        if (definition.StatPoints > 0) ImGui.TextUnformatted($"Stat points: +{definition.StatPoints} permanent");
        FieldLabel("Duration");
        Wrapped(definition.Effect is { } effect ? TimeSpan.FromSeconds(effect.DurationSeconds).ToString("g") : "—");
        FieldLabel("Emote text");
        Wrapped(string.IsNullOrWhiteSpace(definition.Potion.UseMessage) ? "—" : definition.Potion.UseMessage);
        FieldLabel("Emote Channel");
        if (ImGui.BeginCombo("##MessageDestination", destination < 0 ? "" : ChatCommand.Destinations[destination]))
        {
            if (ImGui.Selectable("None", destination < 0)) destination = -1;
            for (var i = 0; i < ChatCommand.Destinations.Length; i++)
                if (ImGui.Selectable(ChatCommand.Destinations[i], destination == i)) destination = i;
            ImGui.EndCombo();
        }
        if (destination >= 0 && !CanPost(definition.Potion.UseMessage, destination))
            FieldNote("PotionUse", !ChatDestination.Available(player, party, destination)
                ? "Choose an available channel or leave it blank."
                : LiteralChatText.Error(definition.Potion.UseMessage, ChatLimit)
                    ?? "Wait a moment before posting another item message.", "");
    }
}
