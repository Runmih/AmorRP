using AmorRP.Contracts.Seasons;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private SeasonContext? seasonContext;
    private Guid? previouslyActiveSeason, transitionSeason;
    private bool observedSeason, activateSeason;
    private string seasonConfirmation = "";
    private bool CanCreateSeasons => session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow && SeasonSelection.CanCreate(seasonContext);
    private bool IsSeasonAdministrator => session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow && seasonContext?.IsAdministrator == true;
    private bool SeasonReadOnly => SeasonSelection.ReadOnly(community);

    private void ResetSeasons()
    {
        seasonCreationAccessLoaded = false; seasonCreationAccess = new([], null);
        seasonCreationSearch = seasonCreationReason = "";
        seasonContext = null; previouslyActiveSeason = transitionSeason = null;
        observedSeason = false; seasonConfirmation = "";
    }

    private void DrawSeasonState()
    {
        if (SeasonReadOnly) Wrapped(community!.SeasonState == "archived"
            ? $"{community.Name} · Archived, read-only"
            : $"{community.Name} · Draft, read-only");
    }

    private void DrawNoSeason() => Wrapped(seasonContext == null
        ? "Loading seasons…"
        : "No season is active. A server administrator can activate one from Season management.");

    private void DrawSeasonManagement()
    {
        if (!CanCreateSeasons && !IsSeasonAdministrator) return;
        Wrapped("One season is active at a time. Activating another archives the current season. Each season keeps its own groups, items, finances and records.");
        foreach (var entry in seasonContext!.Seasons)
        {
            ImGui.PushID(entry.Id.ToString()); ImGui.Separator();
            ImGui.TextUnformatted(entry.Name); ImGui.SameLine(); ImGui.TextDisabled(entry.State);
            ImGui.BeginDisabled(WriteBlocked);
            if (IsSeasonAdministrator && entry.State == "draft" && ImGui.SmallButton("Activate season"))
            { transitionSeason = entry.Id; activateSeason = true; seasonConfirmation = ""; }
            if (IsSeasonAdministrator && entry.State == "active" && ImGui.SmallButton("Archive season"))
            { transitionSeason = entry.Id; activateSeason = false; seasonConfirmation = ""; }
            ImGui.EndDisabled(); ImGui.PopID();
        }
        var target = seasonContext.Seasons.FirstOrDefault(x => x.Id == transitionSeason);
        if (!IsSeasonAdministrator || target == null) return;
        ImGui.Separator();
        Wrapped(activateSeason
            ? "This season keeps its own assets and records. The current season will become a read-only archive."
            : "This season will become a read-only archive. Open trades end and release their reservations. No season will be active until you activate another.");
        Wrapped("Type the exact name to confirm: " + target.Name);
        ImGui.InputText("##SeasonConfirmation", ref seasonConfirmation, 512);
        ImGui.BeginDisabled(WriteBlocked || seasonConfirmation != target.Name);
        if (ImGui.Button(activateSeason ? "Confirm activation" : "Confirm archive"))
            Mutate("POST", $"api/v2/seasons/{target.Id}/{(activateSeason ? "activation" : "archive")}",
                new SeasonTransition(seasonConfirmation), ETag("season", target.Id, target.Version));
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Cancel transition")) { transitionSeason = null; seasonConfirmation = ""; }
    }
}
