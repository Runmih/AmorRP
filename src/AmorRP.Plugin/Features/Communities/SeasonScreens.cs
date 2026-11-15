using AmorRP.Contracts.Communities;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private SeasonContext? seasonContext;
    private Guid? previouslyActiveSeason, transitionSeason;
    private bool observedSeason, activateSeason;
    private string seasonConfirmation = "";
    private bool SeasonsEnabled => seasonContext?.Enabled == true;
    private bool IsSeasonAdministrator => session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow && seasonContext?.IsAdministrator == true;
    private bool SeasonReadOnly => SeasonSelection.ReadOnly(SeasonsEnabled, community);
    private string SeasonLabel => SeasonsEnabled ? "season" : "community";
    private void ResetSeasons()
    {
        seasonContext = null; previouslyActiveSeason = transitionSeason = null;
        observedSeason = false; seasonConfirmation = "";
    }

    private void DrawSeasonState()
    {
        if (SeasonsEnabled && community?.SeasonState == "active") ImGui.TextDisabled("Active season · Groups, items and finances belong to this season.");
        if (SeasonReadOnly) Wrapped(community!.SeasonState == "archived"
            ? "Archived season · Read-only. Its items, finances and records stay here."
            : "This season is not active. Items and finances can change only in the active season.");
    }

    private void DrawNoSeason()
    {
        if (SeasonsEnabled)
            Wrapped(seasonContext?.ActiveSeasonId == null
                ? "No season is active yet. A server administrator will open the next season."
                : "Your membership in the active season is unavailable. Contact its owner if you were removed or blocked.");
        else
        {
            Wrapped("Choose a community to get started.");
            if (ImGui.Button("Join community")) OpenJoinCommunity?.Invoke();
            ImGui.SameLine(); if (ImGui.Button("Create community")) OpenCreateCommunity?.Invoke();
        }
    }

    private void DrawSeasonManagement()
    {
        if (!IsSeasonAdministrator) return;
        Wrapped("One season is active at a time. Activating a new season archives the current one. Each season keeps its own groups, items, wallets, bills and records.");
        ImGui.BeginDisabled(WriteBlocked);
        if (ImGui.Button("Prepare new season")) OpenCreateCommunity?.Invoke();
        ImGui.EndDisabled();
        foreach (var entry in seasonContext!.Seasons)
        {
            ImGui.PushID(entry.Id.ToString());
            ImGui.Separator(); ImGui.TextUnformatted(entry.Name); ImGui.SameLine(); ImGui.TextDisabled(entry.State);
            ImGui.BeginDisabled(WriteBlocked);
            if (entry.State is "draft" or "legacy" && ImGui.SmallButton(entry.State == "legacy" ? "Activate existing community" : "Activate season"))
            { transitionSeason = entry.Id; activateSeason = true; seasonConfirmation = ""; }
            if (entry.State == "active" && ImGui.SmallButton("Archive season"))
            { transitionSeason = entry.Id; activateSeason = false; seasonConfirmation = ""; }
            ImGui.EndDisabled(); ImGui.PopID();
        }
        var target = seasonContext.Seasons.FirstOrDefault(x => x.Id == transitionSeason);
        if (target == null) return;
        ImGui.Separator();
        Wrapped(activateSeason
            ? (target.State == "legacy" ? "Activating this existing community preserves its economy and enrolls verified players. " : "This fresh season starts with separate wallets and assets. ") + "The current season will become a read-only archive."
            : "This season will become a read-only archive. Open trades will end and release their reservations. No season will be active until you activate another one.");
        Wrapped("Type the exact name to confirm: " + target.Name);
        ImGui.InputText("##SeasonConfirmation", ref seasonConfirmation, 512);
        ImGui.BeginDisabled(WriteBlocked || seasonConfirmation != target.Name);
        if (ImGui.Button(activateSeason ? "Confirm activation" : "Confirm archive"))
            Mutate("POST", $"api/v2/seasons/{target.Id}/{(activateSeason ? "activation" : "archive")}",
                new SeasonTransition(seasonConfirmation), ETag("community", target.Id, target.Version));
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Cancel transition")) { transitionSeason = null; seasonConfirmation = ""; }
    }
}
