using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    public MoodleAdapter? Moodles { private get; set; }
    private readonly BackgroundRefresh<MoodleGrants> moodleRefresh = new(TimeSpan.FromSeconds(5));
    private readonly BackgroundRefresh<OwnershipEffectPolicy> ownershipEffectRefresh = new(TimeSpan.FromSeconds(30));
    private OwnershipEffectPolicy? ownershipEffectPolicy;
    private MoodleEffect? ownershipEffectDraft, recipeEffectDraft;

    private void TickMoodles()
    {
        var now = DateTimeOffset.UtcNow;
        if (Moodles == null || origin is not { } uri || session is not { } actor ||
            actor.AccessExpiresAt <= now || !serviceCompatible) return;
        if (Moodles.IsReady(now))
        {
            if (moodleRefresh.TryCollect(now, dataGeneration, out var grants))
                Moodles.Reconcile($"{uri}|{actor.Character.Id}", actor.Character.DisplayName, actor.Character.HomeWorldName, grants, now);
            moodleRefresh.Start(now, dataGeneration, requests.Token,
                ct => client.GetAsync<MoodleGrants>(uri, "api/v2/me/moodle-effects", actor.AccessToken, ct));
        }
        else if (moodleRefresh.IsLoading || moodleRefresh.HasLoaded) moodleRefresh.Reset();

        if (!Owner || community is not { } season) return;
        if (ownershipEffectRefresh.TryCollect(now, dataGeneration, out var policy))
        {
            if (ownershipEffectPolicy?.Version != policy.Version) ownershipEffectDraft = policy.Effect;
            ownershipEffectPolicy = policy;
        }
        ownershipEffectRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<OwnershipEffectPolicy>(uri, $"api/v2/seasons/{season.Id}/ownership-effect", actor.AccessToken, ct));
    }

    private void ResetMoodles()
    {
        moodleRefresh.Reset();
        ownershipEffectRefresh.Reset();
        ownershipEffectPolicy = null;
        ownershipEffectDraft = null;
    }

    private void DrawOwnershipEffect()
    {
        DrawRefreshState(ownershipEffectRefresh, "Ownership effect");
        if (community is not { } season || ownershipEffectPolicy is not { } policy) return;
        ImGui.BeginDisabled(SeasonReadOnly || WriteBlocked);
        var valid = DrawMoodleEditor("Ownership effect", ref ownershipEffectDraft, ownership: true);
        ImGui.BeginDisabled(!valid || ownershipEffectDraft == policy.Effect);
        if (ActionButton("Save ownership effect"))
            Mutate("PUT", $"api/v2/seasons/{season.Id}/ownership-effect", new OwnershipEffectPolicy(policy.Version, ownershipEffectDraft));
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }

    private bool DrawMoodleEditor(string label, ref MoodleEffect? draft, bool ownership = false)
    {
        ImGui.PushID(label);
        var enabled = draft != null;
        if (ImGui.Checkbox(label, ref enabled))
            draft = enabled ? new("", "", 200001, ownership ? 90 : 60, ownership) : null;

        if (draft is not { } effect)
        {
            ImGui.PopID();
            return true;
        }

        var name = effect.Name;
        var description = effect.Description;
        var icon = effect.IconId;
        var duration = effect.DurationSeconds;
        var debuff = effect.Debuff;
        ImGui.InputText("Effect name", ref name, 321);
        ImGui.InputTextMultiline("Effect description", ref description, 1201, new(-1, ImGui.GetTextLineHeightWithSpacing() * 3));
        ImGui.InputInt("Status icon ID", ref icon);
        if (ownership) ImGui.TextDisabled("Displayed while the permit is active.");
        else ImGui.InputInt("Duration (seconds)", ref duration);
        if (!ownership) ImGui.Checkbox("Debuff", ref debuff);
        draft = new(name, description, icon, duration, debuff);

        var iconInRange = icon is >= 200000 and <= 299999;
        ISharedImmediateTexture? texture = null;
        var hasIcon = iconInRange && textures.TryGetFromGameIcon(new GameIconLookup((uint)icon), out texture);
        if (hasIcon && texture != null && texture.TryGetWrap(out var wrap, out _))
        {
            ImGui.Image(wrap.Handle, new(24, 32));
            ImGui.SameLine();
        }
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(name) ? "Effect preview" : name);
        if (description.Length > 0) Wrapped(description);
        var valid = PlainText.Error(name, 80) == null && PlainText.Error(description, 300, multiline: true, required: false) == null &&
            !name.Contains('[') && !description.Contains('[') && hasIcon && duration >= 15 && duration <= 86400;
        if (!valid) ImGui.TextDisabled("Use plain text, a current status icon, and a duration from 15 to 86,400 seconds.");
        ImGui.PopID();
        return valid;
    }
}
