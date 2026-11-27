using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<StatusEffect[]> statusEffectRefresh = new(TimeSpan.FromSeconds(20));
    private StatusEffect[] statusEffects = [];
    private StatusEffect? statusDraft;
    private static readonly string[] StatusModifierIds = ["display", "ac", "attack", "check"];
    private static readonly string[] StatusModifierLabels = ["Moodle only", "Armor class", "Attack rolls", "Ability checks"];

    private string StatusEffectsPath => $"api/v2/seasons/{community!.Id}/status-effects";

    private void ResetStatusEffects()
    {
        statusEffectRefresh.Reset();
        statusEffects = [];
        statusDraft = null;
    }

    private void ReadStatusEffects()
    {
        var now = DateTimeOffset.UtcNow;
        if (statusEffectRefresh.TryCollect(now, dataGeneration, out var result)) statusEffects = result;
        if (community == null || session is not { } actor || origin is not { } uri) return;
        var path = StatusEffectsPath;
        statusEffectRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<StatusEffect[]>(uri, path, actor.AccessToken, ct));
    }

    private void AcceptStatusEffect(StatusEffect saved)
    {
        statusEffects = [.. statusEffects.Where(effect => effect.Id != saved.Id), saved];
        statusDraft = saved;
        statusEffectRefresh.Invalidate();
    }

    private void DrawStatusEffects()
    {
        ReadStatusEffects();
        DrawRefreshState(statusEffectRefresh, "Status effects");
        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly || !statusEffectRefresh.HasLoaded);
        if (ImGui.Button("New status effect"))
            statusDraft = new(Guid.NewGuid(), 0, "", 300, "ac", 2, null, null);
        foreach (var effect in statusEffects.OrderBy(effect => effect.Name, StringComparer.OrdinalIgnoreCase))
            if (ImGui.Selectable(StatusLabel(effect) + "###Status_" + effect.Id, statusDraft?.Id == effect.Id))
                statusDraft = effect;

        if (statusDraft is { } draft)
        {
            var latest = statusEffects.FirstOrDefault(effect => effect.Id == draft.Id);
            var name = draft.Name;
            var duration = draft.DurationSeconds;
            var modifier = Math.Max(0, Array.IndexOf(StatusModifierIds, draft.Modifier));
            var amount = draft.Amount == 0 ? 1 : draft.Amount;
            var ability = Math.Max(0, Array.IndexOf(SkillAbilities, draft.Ability));
            var moodle = draft.Moodle;
            ImGui.InputText("Effect name", ref name, 321);
            ImGui.InputInt("Duration (seconds)", ref duration);
            duration = Math.Clamp(duration, 15, 86400);
            ImGui.Combo("Modifier", ref modifier, StatusModifierLabels, StatusModifierLabels.Length);
            if (modifier == 3) ImGui.Combo("Ability check", ref ability, SkillAbilities, SkillAbilities.Length);
            if (modifier != 0) ImGui.InputInt("Bonus or penalty", ref amount);
            var validMoodle = DrawMoodleEditor("Show a Moodle", ref moodle, durationOverride: duration);

            statusDraft = draft with
            {
                Name = name, DurationSeconds = duration, Modifier = StatusModifierIds[modifier],
                Amount = modifier == 0 ? 0 : Math.Clamp(amount, -10, 10),
                Ability = modifier == 3 ? SkillAbilities[ability] : null, Moodle = moodle
            };

            var stale = latest != null && latest.Revision != draft.Revision;
            if (stale && ImGui.Button("Reload saved effect")) statusDraft = latest;
            var invalid = PlainText.Error(name, 80) != null || !validMoodle || (modifier == 0 ? moodle == null : amount == 0);
            ImGui.BeginDisabled(stale || invalid || statusDraft == latest);
            if (statusDraft != null && ActionButton("Save status effect"))
                Mutate("PUT", StatusEffectsPath + "/" + draft.Id, statusDraft);
            ImGui.EndDisabled();
        }
        ImGui.EndDisabled();
    }

    private static string StatusLabel(StatusEffect effect) => effect.Name.Replace("##", "# #");

    private static string StatusSummary(StatusEffect effect)
    {
        var modifier = effect.Modifier switch
        {
            "ac" => $"{Signed(effect.Amount)} AC",
            "attack" => $"{Signed(effect.Amount)} to attack rolls",
            "check" => $"{Signed(effect.Amount)} to {effect.Ability} checks",
            _ => "Moodle only"
        };
        return $"{modifier} · {TimeSpan.FromSeconds(effect.DurationSeconds):g}"
            + (effect.Moodle is { } visual ? $" · {visual.Name} Moodle" : "");
    }

    private StatusEffectReference? DrawStatusSelector(StatusEffectReference? selected, StatusEffect? pinned = null)
    {
        ReadStatusEffects();
        var published = statusEffects.FirstOrDefault(effect => effect.Id == selected?.Id);
        var label = selected == null ? "None" : published != null ? StatusLabel(published) : pinned?.Name ?? "Saved status effect";
        if (selected != null && published?.Revision != selected.Revision) label += $" (revision {selected.Revision})";
        if (ImGui.BeginCombo("Status effect", label))
        {
            if (ImGui.Selectable("None", selected == null)) selected = null;
            foreach (var effect in statusEffects)
                if (ImGui.Selectable(StatusLabel(effect) + "###PickStatus_" + effect.Id, effect.Reference() == selected))
                    selected = effect.Reference();
            ImGui.EndCombo();
        }
        var preview = statusEffects.FirstOrDefault(effect => effect.Reference() == selected)
            ?? (pinned?.Reference() == selected ? pinned : null);
        if (preview != null) ImGui.TextDisabled(StatusSummary(preview));
        return selected;
    }
}
