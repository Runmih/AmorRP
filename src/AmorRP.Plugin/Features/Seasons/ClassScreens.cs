using AmorRP.Contracts.Seasons;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<ClassSelection> classRefresh = new(TimeSpan.FromSeconds(20));
    private readonly HashSet<Guid> classChoices = [];
    private ClassSelection? classState;
    private ClassPolicy? classDraft;
    private Guid? editingClass;
    private bool editCharacterClasses;

    private void ResetClasses()
    {
        classRefresh.Reset();
        classState = null;
        classDraft = null;
        editingClass = null;
        classChoices.Clear();
        editCharacterClasses = false;
    }

    private static string ClassGrantName(string grant) => grant switch
    {
        "items.potion.create" => "Craft Alchemy items",
        "items.cooking.create" => "Craft Cooking items",
        CraftingTypes.GatherPermission => "Gathering and Hunting",
        CraftingTypes.BlacksmithPermission => "Craft Blacksmithing items",
        _ => grant
    };

    private void TickClasses()
    {
        var now = DateTimeOffset.UtcNow;
        if (classRefresh.TryCollect(now, dataGeneration, out var result))
        {
            if (classState?.PolicyVersion != result.PolicyVersion)
            {
                classDraft = result.Policy;
                editingClass = null;
            }
            if (classState?.PolicyVersion != result.PolicyVersion || classState.SelectionVersion != result.SelectionVersion)
            {
                classChoices.Clear();
                classChoices.UnionWith(result.Selected.Where(id => result.Policy.Classes.Any(c => c.Id == id)));
                editCharacterClasses = false;
            }
            classState = result;
        }
        if (origin is not { } uri || session is not { } actor || community is not { } season ||
            actor.AccessExpiresAt <= now || !serviceCompatible) return;
        classRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<ClassSelection>(uri, $"api/v2/seasons/{season.Id}/classes", actor.AccessToken, ct));
    }

    private void DrawCharacterClasses()
    {
        DrawSectionHeading("Classes");
        if (classState is not { } state || state.Selected.Length == 0)
        {
            DrawClassSelection();
            return;
        }

        var selected = state.Policy.Classes.Where(c => state.Selected.Contains(c.Id)).ToArray();
        foreach (var c in selected)
        {
            ImGui.TextUnformatted(c.Name);
            DrawClassDetails(c);
        }
        var canAdd = selected.Length < state.Policy.Slots && selected.Length < state.Policy.Classes.Length;
        if (SeasonReadOnly || !(state.FreeCorrection || canAdd)) return;

        if (ImGui.SmallButton(editCharacterClasses ? "Close class selection" : state.FreeCorrection ? "Correct class selection" : "Choose another class"))
        {
            editCharacterClasses = !editCharacterClasses;
            if (!editCharacterClasses)
            {
                classChoices.Clear();
                classChoices.UnionWith(selected.Select(c => c.Id));
            }
        }
        if (editCharacterClasses) DrawClassSelection();
    }

    private static void DrawClassDetails(SeasonClass c)
    {
        if (!string.IsNullOrWhiteSpace(c.Description)) Wrapped(c.Description);
        if (c.Grants.Length > 0) ImGui.TextDisabled(string.Join(" · ", c.Grants.Select(ClassGrantName)));
    }

    private void DrawClassSelection()
    {
        DrawRefreshState(classRefresh, "Classes");
        if (classState is not { } state || community is not { } season) return;
        if (state.Policy.Classes.Length == 0 && !state.FreeCorrection)
        {
            ImGui.TextDisabled("No classes are available this season.");
            return;
        }
        ImGui.TextUnformatted($"{classChoices.Count} / {state.Policy.Slots} class slots selected");
        if (state.FreeCorrection) Wrapped("The owner changed the class options. You can correct your selection for free.");
        foreach (var c in state.Policy.Classes)
        {
            ImGui.PushID(c.Id.ToString());
            var checkedNow = classChoices.Contains(c.Id);
            if (ImGui.Checkbox(c.Name.Replace("##", "# #") + "###ClassChoice", ref checkedNow))
            {
                if (checkedNow) classChoices.Add(c.Id);
                else classChoices.Remove(c.Id);
            }
            DrawClassDetails(c);
            ImGui.PopID();
        }
        var existing = state.Selected.Where(id => state.Policy.Classes.Any(c => c.Id == id)).ToArray();
        var preservesClasses = classChoices.Intersect(existing).Count() >= Math.Min(state.Policy.Slots, existing.Length);
        if (!preservesClasses) Wrapped("Use a Character Reset Potion before replacing chosen classes.");
        var canSave = preservesClasses && classChoices.Count <= state.Policy.Slots && !classChoices.SetEquals(state.Selected);
        ImGui.BeginDisabled(!canSave);
        if (ActionButton("Confirm class selection"))
            Mutate("PUT", $"api/v2/seasons/{season.Id}/classes/selection",
                new ClassSelectionEdit(state.PolicyVersion, state.SelectionVersion, classChoices.Order().ToArray(), 0));
        ImGui.EndDisabled();
    }

    private void DrawClassPolicy()
    {
        DrawRefreshState(classRefresh, "Class settings");
        if (classState is not { } state || classDraft is not { } draft || community is not { } season) return;
        var slots = draft.Slots;
        var cap = draft.GroupRecipeLimit;
        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
        ImGui.BeginDisabled(draft.Classes.Length >= 32);
        if (ImGui.Button("Add class"))
        {
            var c = new SeasonClass(Guid.NewGuid(), "New class", "", []);
            draft = draft with { Classes = [.. draft.Classes, c] };
            editingClass = c.Id;
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8);
        if (ImGui.InputInt("Class slots", ref slots)) draft = draft with { Slots = Math.Clamp(slots, 1, 8) };
        if (ImGui.InputInt("Active recipes per group", ref cap)) draft = draft with { GroupRecipeLimit = Math.Clamp(cap, 0, 100) };

        foreach (var c in draft.Classes)
            if (ImGui.Selectable(c.Name.Replace("##", "# #") + "###Class_" + c.Id, editingClass == c.Id)) editingClass = c.Id;
        var index = Array.FindIndex(draft.Classes, c => c.Id == editingClass);
        if (index >= 0)
        {
            var c = draft.Classes[index];
            var name = c.Name;
            var description = c.Description;
            if (ImGui.InputText("Class name", ref name, 321)) c = c with { Name = name };
            if (ImGui.InputTextMultiline("Description", ref description, 1201, new(-1, ImGui.GetTextLineHeightWithSpacing() * 3)))
                c = c with { Description = description };
            foreach (var grant in new[] { "items.potion.create", "items.cooking.create", CraftingTypes.GatherPermission, CraftingTypes.BlacksmithPermission })
            {
                var enabled = c.Grants.Contains(grant);
                if (ImGui.Checkbox(ClassGrantName(grant), ref enabled))
                    c = c with { Grants = enabled ? [.. c.Grants, grant] : c.Grants.Where(g => g != grant).ToArray() };
            }
            if (ImGui.Button("Remove class"))
            {
                draft = draft with { Classes = draft.Classes.Where(x => x.Id != editingClass).ToArray() };
                editingClass = null;
            }
            else if (draft.Classes[index] != c)
            {
                var classes = draft.Classes.ToArray();
                classes[index] = c;
                draft = draft with { Classes = classes };
            }
        }
        classDraft = draft;
        ImGui.BeginDisabled(draft == state.Policy ||
            draft.Classes.Any(c => PlainText.Error(c.Name, 80) != null || PlainText.Error(c.Description, 300, multiline: true, required: false) != null));
        if (ActionButton("Save class settings"))
            Mutate("PUT", $"api/v2/seasons/{season.Id}/classes/policy", new ClassPolicyEdit(state.PolicyVersion, draft));
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }

}
