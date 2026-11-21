using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private string noticeSearch = "";

    private void DrawNoticeboard()
    {
        if (community == null) { DrawNoSeason(); return; }
        DrawRefreshState(hubRefresh, "Noticeboard");
        UiIcons.SearchLabel();
        ImGui.SetNextItemWidth(Math.Max(70, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 1.6f));
        LimitedInput("##FilterNotices", ref noticeSearch, 100, hint: "Filter notices…");
        ImGui.SameLine();
        ImGui.BeginDisabled(noticeSearch.Length == 0);
        if (UiIcons.ClearButton("Clear notice filter")) noticeSearch = "";
        ImGui.EndDisabled();

        var now = DateTimeOffset.UtcNow;
        RefreshGroupCardLogos(now);
        var found = false;
        foreach (var item in hub.Notices)
        {
            if (!(item.Title + " " + item.Message + " " + item.GroupName + " " + item.Reward)
                .Contains(noticeSearch, StringComparison.OrdinalIgnoreCase)) continue;
            found = true;
            var group = communityGroups.FirstOrDefault(group => group.Id == item.GroupId);
            DrawGroupCard("NoticeCard", item.GroupId, item.Title, item.GroupName, group, now, [item.Message], item.Reward);
        }
        if (hub.Notices.Length == 0) Wrapped("No requests posted yet. Group owners and authorized staff can publish one from their group's Notice tab.");
        else if (!found) ImGui.TextDisabled("No matching notices.");
    }
}
