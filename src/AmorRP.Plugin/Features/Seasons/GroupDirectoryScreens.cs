using System.Numerics;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private bool MatchesGroupFilter(Group group) =>
        (!favoriteGroupsOnly || group.IsFavorite) && group.Name.Contains(groupSearch.Trim(), StringComparison.OrdinalIgnoreCase);

    private void DrawGroupDirectory()
    {
        if (requestedDirectoryGroup is { } requested && communityGroups.FirstOrDefault(group => group.Id == requested) is { } target)
        {
            requestedDirectoryGroup = null;
            if (!MatchesGroupFilter(target))
            {
                groupSearch = "";
                favoriteGroupsOnly = false;
            }
            RequestGroupChange(target);
        }

        UiIcons.SearchLabel();
        ImGui.SetNextItemWidth(Math.Max(60, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 1.6f));
        LimitedInput("##GroupSearch", ref groupSearch, 100, hint: "Search groups…");
        ImGui.SameLine();
        ImGui.BeginDisabled(groupSearch.Length == 0);
        if (UiIcons.ClearButton("Clear group search")) groupSearch = "";
        ImGui.EndDisabled();
        ImGui.Checkbox("Favorites only", ref favoriteGroupsOnly);

        var rows = communityGroups.Where(MatchesGroupFilter).OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (rows.Length == 0)
        {
            Wrapped(favoriteGroupsOnly ? "No matching favorites." : communityGroups.Length == 0 ? "No groups yet." : "No matching groups.");
            return;
        }
        if (!ImGui.BeginTable("GroupDirectoryRows", 2, ImGuiTableFlags.RowBg)) return;
        ImGui.TableSetupColumn("Group", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Favorite", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        Group? selected = null;
        foreach (var group in rows)
        {
            ImGui.PushID(group.Id.ToString());
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (ImGui.Selectable(group.Name + "###nested" + group.Id, selectedGroupId == group.Id))
                selected = group;
            ImGui.TableNextColumn();
            DrawFavoriteStar(group);
            ImGui.PopID();
        }
        ImGui.EndTable();
        if (selected != null) RequestGroupChange(selected);
    }

    private void DrawFavoriteStar(Group group)
    {
        var size = ImGui.GetFrameHeight();
        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        var clicked = ImGui.Button("##FavoriteGroup", new Vector2(size));
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(group.IsFavorite ? "Remove from favorites" : "Add to favorites");
        if (ImGui.IsItemVisible())
        {
            var center = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
            var color = group.IsFavorite ? ImGui.GetColorU32(InventoryGold) : ImGui.GetColorU32(ImGuiCol.Text);
            var draw = ImGui.GetWindowDrawList();
            var previous = center - new Vector2(0, size * .35f);
            for (var point = 1; point <= 10; point++)
            {
                var angle = point * (MathF.PI / 5) - MathF.PI / 2;
                var radius = size * (point % 2 == 0 ? .35f : .16f);
                var next = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                if (group.IsFavorite) draw.AddTriangleFilled(center, previous, next, color);
                else draw.AddLine(previous, next, color, Math.Max(1, size * .06f));
                previous = next;
            }
        }
        ImGui.EndDisabled();
        if (clicked) Mutate(group.IsFavorite ? "DELETE" : "PUT", GroupPath(group) + "/favorite", null);
    }
}
