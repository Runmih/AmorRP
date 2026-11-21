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

        if (community == null && selected == null) { DrawNoSeason(); return; }
        RefreshGroupCardLogos(now);
        if (selected != null) DrawEventCard(selected, now);
        else if (selectedEventId.HasValue) ImGui.TextDisabled("This event is no longer available.");

        foreach (var item in upcoming)
            if (item.Id != selected?.Id) DrawEventCard(item, now);

        if (selected == null && upcoming.Length == 0 && community != null && hubRefresh.HasLoaded)
            ImGui.TextDisabled("No upcoming events in this season.");
    }

    private void DrawEventCard(GroupEvent item, DateTimeOffset now)
    {
        var schedule = FormattableString.Invariant($"{item.StartsAt.UtcDateTime:yyyy-MM-dd HH:mm} → {item.EndsAt.UtcDateTime:yyyy-MM-dd HH:mm} ST");
        var body = new List<string> { schedule };
        var group = communityGroups.FirstOrDefault(group => group.CommunityId == item.CommunityId && group.Id == item.GroupId);
        if (item.CommunityId != community?.Id && communities.FirstOrDefault(entry => entry.Id == item.CommunityId) is { } season)
            body.Add(season.Name);
        if (!string.IsNullOrWhiteSpace(item.Location)) body.Add(item.Location);
        if (!string.IsNullOrWhiteSpace(item.Description)) body.Add(item.Description);
        DrawGroupCard("EventCard", item.Id, item.Title, item.GroupName, group, now, body);
    }
}
