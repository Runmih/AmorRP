using AmorRP.Contracts.Groups;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private Guid? selectedEventId;

    private void DrawSeasonEvents()
    {
        if (community != null) DrawRefreshState(hubRefresh, "Events");
        var now = DateTimeOffset.UtcNow;
        var upcoming = hub.Events.Where(item => item.EndsAt > now)
            .OrderBy(item => item.StartsAt).ThenBy(item => item.Id).ToArray();
        var selected = upcoming.FirstOrDefault(item => item.Id == selectedEventId);
        // The HUD may link to an event before this season's event list has refreshed.
        // Keep the exact linked event visible without changing the viewed season.
        var favorite = favoriteEvents.Items.FirstOrDefault(item => item.Id == selectedEventId && item.EndsAt > now);
        if (favorite != null && (selected == null || favorite.Version > selected.Version))
            selected = favorite;

        if (selected != null)
        {
            DrawEventDetails(selected);
            ImGui.Spacing();
            ImGui.Separator();
        }
        else if (selectedEventId.HasValue)
            ImGui.TextDisabled("This event is no longer available.");

        if (community == null && selected == null) { DrawNoSeason(); return; }
        if (upcoming.Length == 0)
        {
            if (community != null && hubRefresh.HasLoaded) ImGui.TextDisabled("No upcoming events in this season.");
            return;
        }
        if (!ImGui.BeginTable("SeasonEvents", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH)) return;
        ImGui.TableSetupColumn("Event", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("When", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();
        foreach (var item in upcoming)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.PushID(item.Id.ToString());
            if (ImGui.Selectable(item.Title + "###Event", item.Id == selectedEventId, ImGuiSelectableFlags.SpanAllColumns))
            {
                selectedEventId = item.Id;
                ImGui.SetScrollY(0);
            }
            ImGui.PopID();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(FavoriteEventSchedule.When(item, now));
        }
        ImGui.EndTable();
    }

    private void DrawEventDetails(GroupEvent item)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, InventoryGold);
        Wrapped(item.Title);
        ImGui.PopStyleColor();
        Wrapped(item.GroupName);
        if (item.CommunityId != community?.Id && communities.FirstOrDefault(entry => entry.Id == item.CommunityId) is { } season)
            ImGui.TextDisabled(season.Name);
        ImGui.Spacing();
        Wrapped($"Starts {item.StartsAt.UtcDateTime:yyyy-MM-dd HH:mm} ST");
        Wrapped($"Ends {item.EndsAt.UtcDateTime:yyyy-MM-dd HH:mm} ST");
        ImGui.Spacing();
        Wrapped(item.Location);
        if (item.Description.Length > 0) Wrapped(item.Description);
    }
}
