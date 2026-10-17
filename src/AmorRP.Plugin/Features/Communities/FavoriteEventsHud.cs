using System.Net;
using System.Numerics;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private readonly BackgroundRefresh<FavoriteEvents> favoriteRefresh = new(TimeSpan.FromSeconds(30));
    private FavoriteEvents favoriteEvents = new([]);
    public Action<bool>? SetFavoriteEventsVisible { get; set; }

    private void ClearFavoriteEvents()
    {
        favoriteRefresh.Reset();
        favoriteEvents = new([]);
    }

    private void TickFavoriteEvents()
    {
        if (session == null) { ClearFavoriteEvents(); return; }
        var now = DateTimeOffset.UtcNow;
        if (favoriteRefresh.TryCollect(now, dataGeneration, out var events)) favoriteEvents = events;
        if (!SessionRenewalInProgress && favoriteRefresh.Error is FoundationApiException { Status: HttpStatusCode.Unauthorized })
        { ClearWork(); ClearCommunityViews(); ClearFavoriteEvents(); session = null; return; }
        if (!configuration.ShowFavoriteEvents) { favoriteRefresh.Reset(); return; }
        if (SessionRenewalInProgress || session == null || origin == null || !serviceCompatible || WriteBlocked || session.AccessExpiresAt <= now) return;
        var uri = origin; var token = session.AccessToken;
        favoriteRefresh.Start(now, dataGeneration, lifetime.Token,
            ct => client.GetAsync<FavoriteEvents>(uri, "api/v2/me/favorite-group-events", token, ct));
    }

    public void DrawFavoriteEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var upcoming = session == null ? [] : FavoriteEventSchedule.Upcoming(favoriteEvents.Items, now);
        var header = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##MoveEventsHud", new(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        var draw = ImGui.GetWindowDrawList();
        draw.AddText(header, ImGui.GetColorU32(ImGuiCol.Text), "Favorite events");
        var count = upcoming.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        draw.AddText(new(header.X + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(count).X, header.Y),
            ImGui.GetColorU32(upcoming.Length > 0 ? ImGuiCol.PlotHistogram : ImGuiCol.TextDisabled), count);
        ImGui.TextDisabled("Next 5 days · Server Time");
        if (session == null) { Wrapped("Sign in to Amor RP to see events."); return; }
        DrawRefreshState(favoriteRefresh, "Events");
        if (upcoming.Length == 0)
        {
            if (favoriteRefresh.HasLoaded) Wrapped("No events in the next five days. Favorite groups from the Groups tab to follow their events.");
            return;
        }

        var rowHeight = ImGui.GetTextLineHeightWithSpacing() * 3 + ImGui.GetStyle().ItemSpacing.Y;
        ImGui.BeginChild("FavoriteEventsList", new(0,
            (rowHeight + ImGui.GetStyle().ItemSpacing.Y) * Math.Min(4, upcoming.Length)), false);
        foreach (var item in upcoming)
        {
            ImGui.PushID(item.Id.ToString());
            var position = ImGui.GetCursorScreenPos();
            if (ImGui.Selectable("##Event", false, ImGuiSelectableFlags.None, new(0, rowHeight))) ImGui.OpenPopup("EventDetails");
            var line = ImGui.GetTextLineHeightWithSpacing();
            var color = ImGui.GetColorU32(item.StartsAt <= now ? ImGuiCol.PlotHistogram : ImGuiCol.TextDisabled);
            var list = ImGui.GetWindowDrawList();
            list.AddText(position, color, FavoriteEventSchedule.When(item, now));
            DrawHudLine(list, position + new Vector2(0, line), item.Title, ImGuiCol.Text);
            var communityName = communities.FirstOrDefault(x => x.Id == item.CommunityId)?.Name;
            DrawHudLine(list, position + new Vector2(0, line * 2), item.GroupName + (communityName == null ? "" : " · " + communityName), ImGuiCol.TextDisabled);
            ImGui.SetNextWindowSize(new(ImGui.GetFontSize() * 25, 0), ImGuiCond.Appearing);
            if (ImGui.BeginPopup("EventDetails"))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetFontSize() * 24);
                DrawEvent(item);
                ImGui.PopTextWrapPos();
                if (ImGui.SmallButton("Close")) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
            ImGui.PopID();
        }
        ImGui.EndChild();
    }

    private static void DrawHudLine(ImDrawListPtr draw, Vector2 position, string text, ImGuiCol color)
    {
        var width = Math.Max(20, ImGui.GetContentRegionAvail().X);
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(text);
        var length = boundaries.Length;
        while (length > 0 && ImGui.CalcTextSize(text).X > width)
        {
            length--;
            text = text[..boundaries[length]] + "…";
        }
        draw.AddText(position, ImGui.GetColorU32(color), text);
    }
}
