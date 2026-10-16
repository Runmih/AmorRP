using System.Numerics;
using System.Text;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private string definitionName = "", definitionDescription = "", definitionMessage = "";
    private int definitionCost = 1, definitionCategory;
    private bool definitionEnabled = true;
    private Guid? definitionEditId;
    private int? definitionEditRevision;
    private void SelectRecipe(Definition recipe)
    {
        definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision; definitionName = recipe.Name;
        definitionDescription = recipe.Description; definitionMessage = recipe.Potion.UseMessage; definitionCost = recipe.Potion.CreationCost; definitionEnabled = recipe.Enabled;
        definitionCategory = Math.Max(0, Array.FindIndex(categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains("potion")).ToArray(), x => x.Id == recipe.CategoryId));
    }
    private void NewRecipe()
    { definitionEditId = null; definitionEditRevision = null; definitionName = definitionDescription = definitionMessage = ""; definitionCost = 1; definitionEnabled = true; definitionCategory = 0; }
    private void DrawCatalogEditor()
    {
        if (!CanDefinePotions) return;
        if (!ImGui.BeginTable("RecipeLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Recipes", ImGuiTableColumnFlags.WidthStretch, .35f); ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch, .65f);
        ImGui.TableNextColumn(); ImGui.BeginChild("RecipeList", Vector2.Zero, true);
        ImGui.TextUnformatted("Group recipes");
        if (ImGui.Button("+ New recipe")) NewRecipe();
        foreach (var recipe in definitions)
        {
            ImGui.PushID(recipe.Id.ToString());
            if (ImGui.Selectable(recipe.Name + "###Recipe", definitionEditId == recipe.Id)) SelectRecipe(recipe);
            ImGui.TextDisabled($"{recipe.Potion.CreationCost} points · rev {recipe.Revision} · {(recipe.Retired ? "retired" : recipe.Enabled ? "enabled" : "disabled")}");
            ImGui.PopID();
        }
        if (definitions.Length == 0) Wrapped("No recipes yet. Define one here, then create copies in Potions.");
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("RecipeEditor", Vector2.Zero, true);
        ImGui.PushItemWidth(-1);
        var editing = definitions.FirstOrDefault(x => x.Id == definitionEditId);
        ImGui.TextUnformatted(editing == null ? "New potion recipe" : editing.Name);
        Wrapped("A recipe defines future copies. Existing potions keep their original description and use message.");
        var choices = categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains("potion")).ToArray();
        if (choices.Length == 0) Wrapped("No active potion category is available. Ask the group owner to check the catalog.");
        definitionCategory = Math.Clamp(definitionCategory, 0, Math.Max(0, choices.Length - 1));
        if (choices.Length > 1) { FieldLabel("Category"); ImGui.Combo("##RecipeCategory", ref definitionCategory, choices.Select(x => x.Name).ToArray(), choices.Length); }
        var nameLimit = serviceLimits?.DefinitionNameTextElements ?? 80;
        var descriptionLimit = Math.Min(300, serviceLimits?.DefinitionDescriptionTextElements ?? 300);
        FieldLabel("Potion name"); LimitedInput("##RecipeName", ref definitionName, nameLimit);
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
        FieldLabel("Potion points per unit"); ImGui.InputInt("##RecipeCost", ref definitionCost);
        var costValid = definitionCost >= 1 && definitionCost <= (serviceLimits?.MaxWeeklyPotionPoints ?? 1000000);
        FieldNote("RecipeCost", costValid ? null : "Enter a positive cost within the group's supported point limit.", "Each authorized character spends their own weekly points to create copies.");
        ImGui.Checkbox("Allow members to create copies of this recipe", ref definitionEnabled);
        var stale = editing != null && editing.Revision != definitionEditRevision;
        if (stale) { Wrapped("This recipe changed on the server. Reload it before saving a new revision."); if (ImGui.Button("Reload saved recipe")) SelectRecipe(editing!); }
        if (editing?.Retired == true) Wrapped("This recipe is retired. Its existing copies still work. Select New recipe to define another.");
        ImGui.BeginDisabled(choices.Length == 0 || nameError != null || descriptionError != null || messageError != null || !costValid || stale || editing?.Retired == true);
        if (ActionButton(definitionEditId == null ? "Define recipe" : "Save recipe revision"))
        {
            var body = new DefinitionWrite("potion", definitionName, choices[definitionCategory].Id, definitionDescription, definitionEnabled, new(definitionCost, definitionMessage));
            if (definitionEditId.HasValue) Mutate("PATCH", $"api/v1/groups/{group!.Id}/item-definitions/{definitionEditId}", body, ETag("definition", definitionEditId.Value, definitionEditRevision ?? 0));
            else Mutate("POST", $"api/v1/groups/{group!.Id}/item-definitions", body);
        }
        ImGui.EndDisabled();
        if (editing is { Retired: false } && ImGui.CollapsingHeader("Retire this recipe"))
        {
            Wrapped("Stops future production. Existing copies and their messages stay intact.");
            if (ActionButton("Retire selected recipe")) Mutate("POST", $"api/v1/groups/{group!.Id}/item-definitions/{editing.Id}/retirement", null, ETag("definition", editing.Id, editing.Revision));
        }
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
}
