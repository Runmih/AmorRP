using AmorRP.Contracts.Combat;
using AmorRP.Core.Combat;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<SkillLibrary> skillLibraryRefresh = new(TimeSpan.FromSeconds(20));
    private SkillLibrary? skillLibrary;
    private CombatSkill[]? skillDraft;
    private Guid? editingSkill;
    private static readonly string[] SkillAbilities = CombatRules.Abilities.ToArray();
    private static readonly string[] SkillIcons = ["strike", "magic", "heal", "shield", "spark"];
    private static readonly string[] SkillIconNames = ["Strike", "Magic", "Healing", "Shield", "Spark"];
    private static readonly string[] EffectAbilities = ["None", .. SkillAbilities];
    private static FontAwesomeIcon SkillIcon(string icon) => icon switch
    {
        "strike" => FontAwesomeIcon.Khanda,
        "magic" => FontAwesomeIcon.WandSparkles,
        "heal" => FontAwesomeIcon.Heartbeat,
        "shield" => FontAwesomeIcon.ShieldAlt,
        _ => FontAwesomeIcon.Star
    };

    private string SkillLibraryPath => CombatPath + "/skills";

    private void ResetSkillLibrary()
    {
        skillLibraryRefresh.Reset();
        skillLibrary = null;
        skillDraft = null;
        editingSkill = null;
    }

    private void AcceptSkillLibrary(SkillLibrary updated)
    {
        skillLibrary = updated;
        skillDraft = updated.Skills;
        if (!updated.Skills.Any(skill => skill.Id == editingSkill)) editingSkill = null;
        skillLibraryRefresh.Invalidate();
    }

    private void ReadSkillLibrary()
    {
        var now = DateTimeOffset.UtcNow;
        if (skillLibraryRefresh.TryCollect(now, dataGeneration, out var result) && result.Version != skillLibrary?.Version)
        {
            skillLibrary = result;
            skillDraft = result.Skills;
            if (!result.Skills.Any(skill => skill.Id == editingSkill)) editingSkill = null;
        }

        if (origin is not { } uri || session is not { } actor) return;
        var path = SkillLibraryPath;
        skillLibraryRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<SkillLibrary>(uri, path, actor.AccessToken, ct));
    }

    private void DrawSkillLibrary()
    {
        ReadSkillLibrary();
        DrawRefreshState(skillLibraryRefresh, "Skills & Spells");
        if (skillLibrary is not { } saved || skillDraft is not { } draft) return;

        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
        ImGui.BeginDisabled(draft.Length >= 64);
        if (ImGui.Button("Add ability"))
        {
            var skill = new CombatSkill(Guid.NewGuid(), "New ability", "", "skill", "strike", "selected",
                "attack", "STR", 12, "damage", 1, 6, "STR");
            draft = [.. draft, skill];
            editingSkill = skill.Id;
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(draft.SequenceEqual(saved.Skills) || draft.Any(skill =>
            PlainText.Error(skill.Name, 80) != null || PlainText.Error(skill.Description, 300, multiline: true, required: false) != null ||
            (skill.Effect == "status" && skill.StatusEffect == null)));
        if (ActionButton("Save ability library")) Mutate("PUT", SkillLibraryPath, new SkillLibrary(saved.Version, draft));
        ImGui.EndDisabled();

        foreach (var skill in draft)
            if (ImGui.Selectable(SkillLabel(skill) + "###Skill_" + skill.Id, editingSkill == skill.Id))
                editingSkill = skill.Id;

        var index = Array.FindIndex(draft, skill => skill.Id == editingSkill);
        if (index >= 0)
        {
            var edited = DrawSkillFields(draft[index]);
            ImGui.Spacing();
            var inUse = classState?.Policy.Classes.Any(c => c.SkillIds?.Contains(edited.Id) == true) == true;
            ImGui.BeginDisabled(inUse);
            var remove = ImGui.Button("Remove ability");
            if (inUse && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Remove this ability from its classes first.");
            ImGui.EndDisabled();
            if (remove)
            {
                draft = draft.Where(skill => skill.Id != edited.Id).ToArray();
                editingSkill = null;
            }
            else if (edited != draft[index])
            {
                draft = draft.ToArray();
                draft[index] = edited;
            }
        }
        skillDraft = draft;
        ImGui.EndDisabled();
    }

    private static string SkillLabel(CombatSkill skill) => skill.Name.Replace("##", "# #");

    private CombatSkill DrawSkillFields(CombatSkill skill)
    {
        var name = skill.Name;
        var description = skill.Description;
        var kind = skill.Kind == "spell" ? 1 : 0;
        var icon = Math.Max(0, Array.IndexOf(SkillIcons, skill.Icon));
        var target = skill.Target == "self" ? 0 : 1;
        var resolution = skill.Resolution switch { "attack" => 1, "save" => 2, _ => 0 };
        var rollAbility = Math.Max(0, Array.IndexOf(SkillAbilities, skill.RollAbility));
        var saveDc = skill.SaveDc;
        var effect = skill.Effect switch { "healing" => 1, "status" => 2, _ => 0 };
        var dice = skill.DiceCount;
        var sides = skill.DiceSides;
        var effectAbility = Array.IndexOf(SkillAbilities, skill.EffectAbility) + 1;

        ImGui.InputText("Name", ref name, 321);
        ImGui.InputTextMultiline("Description", ref description, 1201, new(-1, ImGui.GetTextLineHeightWithSpacing() * 3));
        ImGui.Combo("Category", ref kind, ["Skill", "Spell"], 2);
        ImGui.Combo("Icon", ref icon, SkillIconNames, SkillIconNames.Length);
        ImGui.Combo("Target", ref target, ["Self", "Selected player"], 2);
        ImGui.Combo("Roll", ref resolution, ["Automatic", "Attack vs. AC", "Target saving throw"], 3);
        if (resolution != 0)
            ImGui.Combo(resolution == 1 ? "Attack ability" : "Target's saving throw", ref rollAbility, SkillAbilities, SkillAbilities.Length);
        if (resolution == 2) ImGui.InputInt("Save DC", ref saveDc);
        ImGui.Combo("Effect", ref effect, ["Damage", "Healing", "Status only"], 3);
        if (effect != 2)
        {
            ImGui.InputInt("Number of dice", ref dice);
            ImGui.InputInt("Sides per die", ref sides);
            ImGui.Combo("Effect modifier", ref effectAbility, EffectAbilities, EffectAbilities.Length);
        }
        var status = DrawStatusSelector(skill.StatusEffect);
        return skill with
        {
            Name = name, Description = description, Kind = kind == 1 ? "spell" : "skill", Icon = SkillIcons[icon],
            Target = target == 0 ? "self" : "selected",
            Resolution = resolution switch { 1 => "attack", 2 => "save", _ => "automatic" },
            RollAbility = SkillAbilities[rollAbility], SaveDc = Math.Clamp(saveDc, 1, 30),
            Effect = effect switch { 1 => "healing", 2 => "status", _ => "damage" },
            DiceCount = effect == 2 ? 0 : Math.Clamp(dice, 1, 20), DiceSides = effect == 2 ? 0 : Math.Clamp(sides, 1, 200),
            EffectAbility = effect == 2 || effectAbility == 0 ? null : SkillAbilities[effectAbility - 1], StatusEffect = status
        };
    }
}
