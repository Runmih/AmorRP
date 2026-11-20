using System.Net;
using System.Numerics;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
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
        if (!configuration.ShowFavoriteEvents || !configuration.ShowAttachedInterfaces) { favoriteRefresh.Reset(); return; }
        if (SessionRenewalInProgress || session == null || origin == null || !serviceCompatible || WriteBlocked || session.AccessExpiresAt <= now) return;
        var uri = origin; var token = session.AccessToken;
        favoriteRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<FavoriteEvents>(uri, "api/v2/me/favorite-group-events", token, ct));
    }

    public GroupEvent[] UpcomingFavoriteEvents() => session == null ? []
        : FavoriteEventSchedule.Upcoming(favoriteEvents.Items, DateTimeOffset.UtcNow);

    public bool DrawFavoriteEvents(GroupEvent[] upcoming)
    {
        var showingDetails = false;
        if (configuration.FavoriteEventsShowHeader) ImGui.TextUnformatted("Favorite Events");
        foreach (var item in upcoming)
        {
            ImGui.PushID(item.Id.ToString());
            var position = ImGui.GetCursorScreenPos();
            if (ImGui.Selectable("##Event")) ImGui.OpenPopup("EventDetails");
            DrawHudLine(ImGui.GetWindowDrawList(), position, item.Title, ImGuiCol.Text);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(item.Title);
            ImGui.SetNextWindowSize(new(ImGui.GetFontSize() * 25, 0), ImGuiCond.Appearing);
            if (ImGui.BeginPopup("EventDetails"))
            {
                showingDetails = true;
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetFontSize() * 24);
                DrawEvent(item);
                ImGui.PopTextWrapPos();
                ImGui.EndPopup();
            }
            ImGui.PopID();
        }
        return showingDetails;
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
