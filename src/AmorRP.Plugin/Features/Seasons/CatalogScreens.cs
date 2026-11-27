using System.Numerics;
using System.Text;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Core.Combat;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private string definitionName = "", definitionDescription = "", definitionMessage = "";
    private int definitionCost = 1, definitionCategory, definitionType;
    private CraftingDiscipline recipeDiscipline;
    private CombatGear? recipeGear;
    private StatusEffectReference? recipeStatus;
    private StatusEffect? pinnedRecipeStatus;
    private int recipeStatPoints;
    private string[] RecipeTypes => CraftingTypes.Types(recipeDiscipline);
    private string RecipeType => RecipeTypes[Math.Clamp(definitionType, 0, RecipeTypes.Length - 1)];
    private void SetRecipeMode(CraftingDiscipline discipline)
    {
        if (recipeDiscipline == discipline) return;
        recipeDiscipline = discipline;
        NewRecipe();
    }
    private bool definitionEnabled = true;
    private Guid? definitionEditId, definitionGroup;
    private int? definitionEditRevision;
    private void SelectRecipe(Definition recipe)
    {
        definitionGroup = recipe.GroupId;
        pinnedRecipeStatus = recipe.StatusEffect;
        recipeStatus = pinnedRecipeStatus?.Reference();
        recipeGear = recipe.Gear;
        recipeStatPoints = recipe.StatPoints;
        definitionType = Math.Max(0, Array.IndexOf(RecipeTypes, recipe.TypeId));
        definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision; definitionName = recipe.Name;
        definitionDescription = recipe.Description; definitionMessage = recipe.Potion.UseMessage; definitionCost = recipe.Potion.CreationCost; definitionEnabled = recipe.Enabled;
        definitionCategory = Math.Max(0, Array.FindIndex(categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains(RecipeType)).ToArray(), x => x.Id == recipe.CategoryId));
    }
    private void NewRecipe()
    { recipeGear = null; recipeStatPoints = 0; recipeStatus = null; pinnedRecipeStatus = null; definitionGroup = null; definitionEditId = null; definitionEditRevision = null; definitionName = definitionDescription = definitionMessage = ""; definitionCost = 1; definitionEnabled = true; definitionCategory = definitionType = 0; }
    private string RecipeAccessLabel(Guid? groupId) => groupId is { } id
        ? communityGroups.FirstOrDefault(group => group.Id == id)?.Name ?? "Group"
        : "Global";

    private void DrawGearRecipeFields(string type)
    {
        if (type == "weapon")
        {
            var gear = recipeGear is { Form: "melee" } current ? current : new CombatGear("melee", "STR", 0, 1, 6);
            var abilities = CombatRules.Abilities.ToArray();
            var ability = Math.Max(0, Array.IndexOf(abilities, gear.Ability));
            var count = gear.DiceCount;
            var sides = gear.DiceSides;
            ImGui.Combo("Attack and damage stat", ref ability, abilities, abilities.Length);
            ImGui.InputInt("Damage dice count", ref count);
            ImGui.InputInt("Sides per die", ref sides);
            recipeGear = new("melee", abilities[ability], 0, Math.Clamp(count, 1, 20), Math.Clamp(sides, 1, 200));
            ImGui.TextDisabled($"Damage: {recipeGear.DiceCount}d{recipeGear.DiceSides} + {recipeGear.Ability} modifier");
        }
        else
        {
            var value = recipeGear is { Form: "armor" } armor ? armor.Value : 12;
            ImGui.InputInt("Armor class", ref value);
            recipeGear = new("armor", null, Math.Clamp(value, 10, 25));
        }
    }

    private void DrawCatalogEditor()
    {
        if (!CanDefine(recipeDiscipline)) return;
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
                        !recipe.Enabled, recipe.Potion, recipe.GroupId, recipe.StatusEffect?.Reference(), recipe.Gear, recipe.StatPoints);
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
        ImGui.TextUnformatted(editing?.Name ?? $"New {recipeDiscipline} recipe");
        FieldLabel("Item type"); ImGui.BeginDisabled(editing != null);
        if (ImGui.Combo("##RecipeType", ref definitionType, RecipeTypes.Select(TypeName).ToArray(), RecipeTypes.Length))
        {
            definitionCategory = 0;
            recipeGear = null;
            if (RecipeType != "potion") recipeStatPoints = 0;
        }
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
        var nameError = PlainText.Error(definitionName, nameLimit)
            ?? (string.Equals(definitionName.Trim(), "Character Reset Potion", StringComparison.OrdinalIgnoreCase)
                ? "Character Reset Potion is a built-in shop product." : null);
        FieldNote("RecipeName", nameError, $"{PlainText.Characters(definitionName)}/{nameLimit} characters");
        FieldLabel("Description"); WrappedInput("##RecipeDescription", ref definitionDescription, 524288, new(-1, ImGui.GetTextLineHeightWithSpacing() * 4), maximumCharacters: descriptionLimit);
        var descriptionError = PlainText.Error(definitionDescription, descriptionLimit, multiline: true, required: false);
        FieldNote("RecipeDescription", descriptionError, $"{PlainText.Characters(definitionDescription)}/{descriptionLimit} characters · optional");
        var isGear = CraftingTypes.IsGear(RecipeType);
        string? messageError = null;
        if (isGear) DrawGearRecipeFields(RecipeType);
        else
        {
            FieldLabel("Message posted when used");
            LimitedInput("##RecipeMessage", ref definitionMessage, ChatLimit, LiteralChatText.MaximumBytes + 1);
            messageError = LiteralChatText.Error(definitionMessage, ChatLimit);
            FieldNote("RecipeMessage", messageError, $"{PlainText.Characters(definitionMessage)}/{ChatLimit} characters · {Encoding.UTF8.GetByteCount(definitionMessage)}/{LiteralChatText.MaximumBytes} bytes · <3 is allowed");
            recipeStatus = DrawStatusSelector(recipeStatus, pinnedRecipeStatus);
            if (RecipeType == "potion" && ImGui.CollapsingHeader("Extra effects", recipeStatPoints > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            {
                var grantPoints = recipeStatPoints > 0;
                if (ImGui.Checkbox("Grant stat points", ref grantPoints)) recipeStatPoints = grantPoints ? 1 : 0;
                if (grantPoints && ImGui.InputInt("Permanent stat points", ref recipeStatPoints))
                    recipeStatPoints = Math.Clamp(recipeStatPoints, 1, 10);
            }
        }
        FieldLabel($"{recipeDiscipline} points per unit"); ImGui.InputInt("##RecipeCost", ref definitionCost);
        var costValid = definitionCost > 0;
        FieldNote("RecipeCost", costValid ? null : "Enter a positive whole-point cost.", "Each authorized character spends their own weekly points to create copies.");
        ImGui.Checkbox("Allow members to create copies of this recipe", ref definitionEnabled);
        var stale = editing != null && editing.Revision != definitionEditRevision;
        if (stale) { Wrapped("This recipe changed on the server. Reload it before saving a new revision."); if (ImGui.Button("Reload saved recipe")) SelectRecipe(editing!); }
        if (editing?.Retired == true) Wrapped("This recipe is retired. Its existing copies still work. Select New recipe to define another.");
        ImGui.BeginDisabled(choices.Length == 0 || nameError != null || descriptionError != null || messageError != null || !costValid || stale || editing?.Retired == true);
        if (ActionButton(definitionEditId == null ? "Define recipe" : "Save recipe revision"))
        {
            var body = new DefinitionWrite(RecipeType, definitionName, choices[definitionCategory].Id, definitionDescription, definitionEnabled, new(definitionCost, isGear ? "" : definitionMessage), definitionGroup,
                isGear ? null : recipeStatus, recipeGear, recipeStatPoints);
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
