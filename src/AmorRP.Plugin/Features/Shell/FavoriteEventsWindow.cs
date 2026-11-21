using System.Numerics;
using AmorRP.Contracts.Groups;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

public sealed class FavoriteEventsWindow(Configuration configuration, Action save, Func<GroupEvent[]> upcoming,
    Action<GroupEvent[]> draw, Func<bool> loaded)
    : Window("Amor RP — Favorite events###AmorRPFavoriteEvents",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus)
{
    private readonly PanelHandle handle = new();
    private LauncherDock dock;
    private bool expanded;
    private GroupEvent[] events = [];

    public override bool DrawConditions()
    {
        if (!loaded()) return false;
        events = upcoming();
        return events.Length > 0 || !configuration.FavoriteEventsHideWhenEmpty;
    }

    public override void PreDraw()
    {
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        var viewport = ImGui.GetMainViewport();
        var scale = ImGuiHelpers.GlobalScale;
        var size = LauncherDock.ClampPercent(configuration.FavoriteEventsSize, 60, 160, 100) / 100;
        expanded = handle.IsExpanded(configuration.FavoriteEventsDocked, configuration.FavoriteEventsEdge, configuration.FavoriteEventsAutoHide);
        var rows = Math.Min(8, events.Length) + (configuration.FavoriteEventsShowHeader ? 1 : 0);
        var contentHeight = Math.Max(1, rows) * ImGui.GetTextLineHeightWithSpacing();
        dock = LauncherDock.PlacePanel(configuration.FavoriteEventsEdge, viewport.WorkPos, viewport.WorkSize,
            configuration.FavoriteEventsPosition, new Vector2(210 * scale * size, contentHeight), scale, expanded,
            configuration.FavoriteEventsDocked, new(configuration.FavoriteEventsFloatX, configuration.FavoriteEventsFloatY), 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.SetNextWindowPos(dock.Position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(dock.Size, ImGuiCond.Always);
    }

    public override void PostDraw() => ImGui.PopStyleVar(2);
    public override void OnClose() => handle.Closed(save);

    public override void Draw()
    {
        var opacity = LauncherDock.ClampPercent(configuration.FavoriteEventsOpacity, 25, 100, 100) / 100;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * opacity);
        try
        {
            if (expanded)
            {
                if (ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup | ImGuiHoveredFlags.ChildWindows)) handle.HoldOpen();
                var background = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
                background.W *= LauncherDock.ClampPercent(configuration.FavoriteEventsBackgroundOpacity, 0, 100, 100) / 100;
                ImGui.GetWindowDrawList().AddRectFilled(dock.Position, dock.Position + dock.Size,
                    ImGui.GetColorU32(background), ImGui.GetStyle().WindowRounding);
            }
            var docked = configuration.FavoriteEventsDocked;
            var position = docked ? new Vector2(configuration.FavoriteEventsPosition, 0)
                : new Vector2(configuration.FavoriteEventsFloatX, configuration.FavoriteEventsFloatY);
            handle.Draw(dock, position, next =>
            {
                if (docked) configuration.FavoriteEventsPosition = next.X;
                else
                {
                    configuration.FavoriteEventsFloatX = next.X;
                    configuration.FavoriteEventsFloatY = next.Y;
                }
            }, save, () => configuration.FavoriteEventsDocked = !configuration.FavoriteEventsDocked);
            if (!expanded) return;

            ImGui.SetCursorScreenPos(dock.Position + dock.ButtonsPosition);
            var available = dock.Size - dock.ButtonsPosition - new Vector2(6 * ImGuiHelpers.GlobalScale);
            if (docked && configuration.FavoriteEventsEdge == LauncherEdge.Bottom) available.Y -= dock.GripSize.Y;
            if (docked && configuration.FavoriteEventsEdge == LauncherEdge.Right) available.X -= dock.GripSize.X;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            try
            {
                if (ImGui.BeginChild("##EventsContent", Vector2.Max(Vector2.One, available), false)) draw(events);
                ImGui.EndChild();
            }
            finally { ImGui.PopStyleColor(); }
        }
        finally { ImGui.PopStyleVar(); }
    }
}
