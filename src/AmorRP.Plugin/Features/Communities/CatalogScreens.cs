using System.Numerics;
using System.Text;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private string definitionName = "", definitionDescription = "", definitionMessage = "";
    private int definitionCost = 1, definitionCategory, definitionType;
    private bool craftCooking;
    private string[] RecipeTypes => craftCooking ? CraftingTypes.Cooking : CraftingTypes.Alchemy;
    private string RecipeType => RecipeTypes[Math.Clamp(definitionType, 0, RecipeTypes.Length - 1)];
    private void SetCraftMode(bool cooking) { if (craftCooking == cooking) return; craftCooking = cooking; recipeChoice = 0; NewRecipe(); }
    private bool definitionEnabled = true;
    private Guid? definitionEditId;
    private int? definitionEditRevision;
    private void SelectRecipe(Definition recipe)
    {
        definitionType = Math.Max(0, Array.IndexOf(RecipeTypes, recipe.TypeId));
        definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision; definitionName = recipe.Name;
        definitionDescription = recipe.Description; definitionMessage = recipe.Potion.UseMessage; definitionCost = recipe.Potion.CreationCost; definitionEnabled = recipe.Enabled;
        definitionCategory = Math.Max(0, Array.FindIndex(categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains(RecipeType)).ToArray(), x => x.Id == recipe.CategoryId));
    }
    private void NewRecipe()
    { definitionEditId = null; definitionEditRevision = null; definitionName = definitionDescription = definitionMessage = ""; definitionCost = 1; definitionEnabled = true; definitionCategory = definitionType = 0; }
    private void DrawCatalogEditor()
    {
        if (!(craftCooking ? CanDefineCooking : CanDefinePotions)) return;
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 38;
        if (!ImGui.BeginTable("RecipeLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Recipes", ImGuiTableColumnFlags.WidthStretch, .35f); if (!narrow) ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch, .65f);
        ImGui.TableNextColumn(); ImGui.BeginChild("RecipeList", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 7 : 0), true);
        ImGui.TextUnformatted("Community recipes");
        if (ImGui.Button("+ New recipe")) NewRecipe();
        foreach (var recipe in definitions.Where(x => RecipeTypes.Contains(x.TypeId)))
        {
            ImGui.PushID(recipe.Id.ToString());
            if (ImGui.Selectable(recipe.Name + "###Recipe", definitionEditId == recipe.Id)) SelectRecipe(recipe);
            ImGui.TextDisabled($"{recipe.Potion.CreationCost} points · rev {recipe.Revision} · {(recipe.Retired ? "retired" : recipe.Enabled ? "enabled" : "disabled")}");
            ImGui.PopID();
        }
        if (!definitions.Any(x => RecipeTypes.Contains(x.TypeId))) Wrapped("No recipes yet. Define one here, then craft it from the matching crafting tab.");
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("RecipeEditor", Vector2.Zero, true);
        ImGui.PushItemWidth(-1);
        var editing = definitions.FirstOrDefault(x => x.Id == definitionEditId);
        ImGui.TextUnformatted(editing == null ? craftCooking ? "New Cooking recipe" : "New Alchemy recipe" : editing.Name);
        Wrapped("A recipe defines future units. Existing items keep their original description and use message.");
        FieldLabel("Item type"); ImGui.BeginDisabled(editing != null);
        if (ImGui.Combo("##RecipeType", ref definitionType, RecipeTypes.Select(TypeName).ToArray(), RecipeTypes.Length)) definitionCategory = 0;
        ImGui.EndDisabled();
        var choices = categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains(RecipeType)).ToArray();
        if (choices.Length == 0) Wrapped("No active category accepts this item type. Ask the community owner to check the catalog.");
        definitionCategory = Math.Clamp(definitionCategory, 0, Math.Max(0, choices.Length - 1));
        if (choices.Length > 1) { FieldLabel("Category"); ImGui.Combo("##RecipeCategory", ref definitionCategory, choices.Select(x => x.Name).ToArray(), choices.Length); }
        var nameLimit = serviceLimits?.DefinitionNameTextElements ?? 80;
        var descriptionLimit = Math.Min(300, serviceLimits?.DefinitionDescriptionTextElements ?? 300);
        FieldLabel("Item name"); LimitedInput("##RecipeName", ref definitionName, nameLimit);
        var nameError = PlainText.Error(definitionName, nameLimit);
        FieldNote("RecipeName", nameError, $"{PlainText.Characters(definitionName)}/{nameLimit} characters");
        FieldLabel("Description"); WrappedInput("##RecipeDescription", ref definitionDescription, 524288, new(-1, ImGui.GetTextLineHeightWithSpacing() * 4), maximumCharacters: descriptionLimit);
        var descriptionError = PlainText.Error(definitionDescription, descriptionLimit, multiline: true, required: false);
        FieldNote("RecipeDescription", descriptionError, $"{PlainText.Characters(definitionDescription)}/{descriptionLimit} characters · optional");
        FieldLabel("Message posted when used"); LimitedInput("##RecipeMessage", ref definitionMessage, ChatLimit, LiteralChatText.MaximumBytes + 1);
        var messageError = LiteralChatText.Error(definitionMessage, ChatLimit);
        FieldNote("RecipeMessage", messageError, $"{PlainText.Characters(definitionMessage)}/{ChatLimit} characters · {Encoding.UTF8.GetByteCount(definitionMessage)}/{LiteralChatText.MaximumBytes} bytes · <3 is allowed");
        ImGui.TextUnformatted("Exact text players will post");
        ImGui.BeginChild("RecipePreview", new(0, ImGui.GetTextLineHeightWithSpacing() * 3), true); Wrapped(definitionMessage.Length == 0 ? "Enter a use message above." : definitionMessage); ImGui.EndChild();
        FieldLabel(craftCooking ? "Cooking points per unit" : "Alchemy points per unit"); ImGui.InputInt("##RecipeCost", ref definitionCost);
        var costValid = definitionCost >= 1 && definitionCost <= (serviceLimits?.MaxWeeklyPotionPoints ?? 1000000);
        FieldNote("RecipeCost", costValid ? null : "Enter a positive cost within the community's supported point limit.", "Each authorized character spends their own weekly points to create copies.");
        ImGui.Checkbox("Allow members to create copies of this recipe", ref definitionEnabled);
        var stale = editing != null && editing.Revision != definitionEditRevision;
        if (stale) { Wrapped("This recipe changed on the server. Reload it before saving a new revision."); if (ImGui.Button("Reload saved recipe")) SelectRecipe(editing!); }
        if (editing?.Retired == true) Wrapped("This recipe is retired. Its existing copies still work. Select New recipe to define another.");
        ImGui.BeginDisabled(choices.Length == 0 || nameError != null || descriptionError != null || messageError != null || !costValid || stale || editing?.Retired == true);
        if (ActionButton(definitionEditId == null ? "Define recipe" : "Save recipe revision"))
        {
            var body = new DefinitionWrite(RecipeType, definitionName, choices[definitionCategory].Id, definitionDescription, definitionEnabled, new(definitionCost, definitionMessage));
            if (definitionEditId.HasValue) Mutate("PATCH", $"api/v2/communities/{community!.Id}/item-definitions/{definitionEditId}", body, ETag("definition", definitionEditId.Value, definitionEditRevision ?? 0));
            else Mutate("POST", $"api/v2/communities/{community!.Id}/item-definitions", body);
        }
        ImGui.EndDisabled();
        if (editing is { Retired: false } && ImGui.CollapsingHeader("Retire this recipe"))
        {
            Wrapped("Stops future production. Existing copies and their messages stay intact.");
            if (ActionButton("Retire selected recipe")) Mutate("POST", $"api/v2/communities/{community!.Id}/item-definitions/{editing.Id}/retirement", null, ETag("definition", editing.Id, editing.Revision));
        }
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
}
