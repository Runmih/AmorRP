using System.Numerics;
using System.Text;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private string definitionName = "", definitionDescription = "", definitionMessage = "";
    private int definitionCost = 1, definitionCategory, definitionType;
    private bool recipeCooking;
    private string[] RecipeTypes => recipeCooking ? CraftingTypes.Cooking : CraftingTypes.Alchemy;
    private string RecipeType => RecipeTypes[Math.Clamp(definitionType, 0, RecipeTypes.Length - 1)];
    private void SetRecipeMode(bool cooking)
    {
        if (recipeCooking == cooking) return;
        recipeCooking = cooking;
        NewRecipe();
    }
    private bool definitionEnabled = true;
    private Guid? definitionEditId, definitionGroup;
    private int? definitionEditRevision;
    private void SelectRecipe(Definition recipe)
    {
        definitionGroup = recipe.GroupId;
        recipeEffectDraft = recipe.Effect;
        definitionType = Math.Max(0, Array.IndexOf(RecipeTypes, recipe.TypeId));
        definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision; definitionName = recipe.Name;
        definitionDescription = recipe.Description; definitionMessage = recipe.Potion.UseMessage; definitionCost = recipe.Potion.CreationCost; definitionEnabled = recipe.Enabled;
        definitionCategory = Math.Max(0, Array.FindIndex(categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains(RecipeType)).ToArray(), x => x.Id == recipe.CategoryId));
    }
    private void NewRecipe()
    { recipeEffectDraft = null; definitionGroup = null; definitionEditId = null; definitionEditRevision = null; definitionName = definitionDescription = definitionMessage = ""; definitionCost = 1; definitionEnabled = true; definitionCategory = definitionType = 0; }
    private string RecipeAccessLabel(Guid? groupId) => groupId is { } id
        ? communityGroups.FirstOrDefault(group => group.Id == id)?.Name ?? "Group"
        : "Global";

    private void DrawCatalogEditor()
    {
        if (!(recipeCooking ? CanDefineCooking : CanDefinePotions)) return;
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 59;
        if (!ImGui.BeginTable("RecipeLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Recipes", ImGuiTableColumnFlags.WidthStretch, .35f); if (!narrow) ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch, .65f);
        ImGui.TableNextColumn(); ImGui.BeginChild("RecipeList", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 7 : 0), true);
        if (ImGui.Button("+ New recipe")) NewRecipe();
        var recipes = definitions.Where(recipe => RecipeTypes.Contains(recipe.TypeId)).ToArray();
        if (ImGui.BeginTable("RecipeRows", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2);
            ImGui.TableSetupColumn("Cost", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Access", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();
            foreach (var recipe in recipes)
            {
                ImGui.PushID(recipe.Id.ToString());
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable(recipe.Name + "###Recipe", definitionEditId == recipe.Id)) SelectRecipe(recipe);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{recipe.Potion.CreationCost} pts");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(RecipeAccessLabel(recipe.GroupId));
                ImGui.TableNextColumn();
                if (recipe.Retired) ImGui.TextDisabled("Retired");
                else if (ActionButton(recipe.Enabled ? "Enabled##RecipeStatus" : "Disabled##RecipeStatus"))
                {
                    var body = new DefinitionWrite(recipe.TypeId, recipe.Name, recipe.CategoryId, recipe.Description,
                        !recipe.Enabled, recipe.Potion, recipe.GroupId, recipe.Effect);
                    Mutate("PATCH", $"api/v2/seasons/{community!.Id}/item-definitions/{recipe.Id}", body,
                        ETag("definition", recipe.Id, recipe.Revision));
                    if (definitionEditId == recipe.Id) definitionEnabled = body.Enabled;
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (recipes.Length == 0) Wrapped("No recipes yet.");
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("RecipeEditor", Vector2.Zero, true);
        ImGui.PushItemWidth(-1);
        var editing = definitions.FirstOrDefault(x => x.Id == definitionEditId);
        ImGui.TextUnformatted(editing == null ? recipeCooking ? "New Cooking recipe" : "New Alchemy recipe" : editing.Name);
        FieldLabel("Item type"); ImGui.BeginDisabled(editing != null);
        if (ImGui.Combo("##RecipeType", ref definitionType, RecipeTypes.Select(TypeName).ToArray(), RecipeTypes.Length)) definitionCategory = 0;
        ImGui.EndDisabled();
        var choices = categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains(RecipeType)).ToArray();
        if (choices.Length == 0) Wrapped("No active category accepts this item type. Ask the season owner to check the catalog.");
        definitionCategory = Math.Clamp(definitionCategory, 0, Math.Max(0, choices.Length - 1));
        if (choices.Length > 1) { FieldLabel("Category"); ImGui.Combo("##RecipeCategory", ref definitionCategory, choices.Select(x => x.Name).ToArray(), choices.Length); }
        var recipeGroups = communityGroups.Where(g => g.IsMember).ToArray();
        var groupName = recipeGroups.FirstOrDefault(g => g.Id == definitionGroup)?.Name;
        FieldLabel("Recipe access");
        if (definitionEditId.HasValue) ImGui.TextUnformatted(groupName ?? (definitionGroup.HasValue ? "Group members" : "Season"));
        else if (ImGui.BeginCombo("##RecipeGroup", groupName ?? "Season"))
        {
            if (ImGui.Selectable("Season", definitionGroup == null)) definitionGroup = null;
            foreach (var group in recipeGroups)
                if (ImGui.Selectable(group.Name.Replace("##", "# #") + "###" + group.Id, definitionGroup == group.Id)) definitionGroup = group.Id;
            ImGui.EndCombo();
        }
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
        FieldLabel(recipeCooking ? "Cooking points per unit" : "Alchemy points per unit"); ImGui.InputInt("##RecipeCost", ref definitionCost);
        var effectValid = DrawMoodleEditor("Moodle effect", ref recipeEffectDraft);
        var costValid = definitionCost > 0;
        FieldNote("RecipeCost", costValid ? null : "Enter a positive whole-point cost.", "Each authorized character spends their own weekly points to create copies.");
        ImGui.Checkbox("Allow members to create copies of this recipe", ref definitionEnabled);
        var stale = editing != null && editing.Revision != definitionEditRevision;
        if (stale) { Wrapped("This recipe changed on the server. Reload it before saving a new revision."); if (ImGui.Button("Reload saved recipe")) SelectRecipe(editing!); }
        if (editing?.Retired == true) Wrapped("This recipe is retired. Its existing copies still work. Select New recipe to define another.");
        ImGui.BeginDisabled(choices.Length == 0 || nameError != null || descriptionError != null || messageError != null || !costValid || !effectValid || stale || editing?.Retired == true);
        if (ActionButton(definitionEditId == null ? "Define recipe" : "Save recipe revision"))
        {
            var body = new DefinitionWrite(RecipeType, definitionName, choices[definitionCategory].Id, definitionDescription, definitionEnabled, new(definitionCost, definitionMessage), definitionGroup, recipeEffectDraft);
            if (definitionEditId.HasValue) Mutate("PATCH", $"api/v2/seasons/{community!.Id}/item-definitions/{definitionEditId}", body, ETag("definition", definitionEditId.Value, definitionEditRevision ?? 0));
            else Mutate("POST", $"api/v2/seasons/{community!.Id}/item-definitions", body);
        }
        ImGui.EndDisabled();
        if (editing is { Retired: false } && ImGui.CollapsingHeader("Retire this recipe"))
        {
            Wrapped("Stops future production. Existing copies and their messages stay intact.");
            if (ActionButton("Retire selected recipe")) Mutate("POST", $"api/v2/seasons/{community!.Id}/item-definitions/{editing.Id}/retirement", null, ETag("definition", editing.Id, editing.Revision));
        }
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
}
