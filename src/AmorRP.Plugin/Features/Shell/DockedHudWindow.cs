using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

internal readonly record struct HudLayout(bool Docked, LauncherEdge Edge, bool AutoHide, float Position,
    Vector2 FloatingPosition, float Width, float Opacity, float BackgroundOpacity, bool ShowHeader, bool HideWhenEmpty)
{
    public Vector2 HandlePosition => Docked ? new(Position, 0) : FloatingPosition;
}

internal sealed class DockedHudWindow<T>(string title, string contentId, Func<HudLayout> layout,
    Action<Vector2> move, Action toggleDock, Action save, Func<T[]> read, Action<T[]> draw, Func<bool> loaded)
    : Window(title,
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus)
{
    private readonly PanelHandle handle = new();
    private LauncherDock dock;
    private bool expanded;
    private T[] rows = [];
    private HudLayout current;

    public override bool DrawConditions()
    {
        if (!loaded()) return false;
        current = layout();
        rows = read();
        return rows.Length > 0 || !current.HideWhenEmpty;
    }

    public override void PreDraw()
    {
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        var viewport = ImGui.GetMainViewport();
        var scale = ImGuiHelpers.GlobalScale;
        var size = LauncherDock.ClampPercent(current.Width, 60, 160, 100) / 100;
        expanded = handle.IsExpanded(current.Docked, current.Edge, current.AutoHide);
        var lineCount = Math.Min(8, rows.Length) + (current.ShowHeader ? 1 : 0);
        var contentHeight = Math.Max(1, lineCount) * ImGui.GetTextLineHeightWithSpacing();
        dock = LauncherDock.PlacePanel(current.Edge, viewport.WorkPos, viewport.WorkSize,
            current.Position, new Vector2(210 * scale * size, contentHeight), scale, expanded,
            current.Docked, current.FloatingPosition, 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.SetNextWindowPos(dock.Position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(dock.Size, ImGuiCond.Always);
    }

    public override void PostDraw() => ImGui.PopStyleVar(2);
    public override void OnClose() => handle.Closed(save);

    public override void Draw()
    {
        var opacity = LauncherDock.ClampPercent(current.Opacity, 25, 100, 100) / 100;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * opacity);
        try
        {
            if (expanded)
            {
                if (ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup | ImGuiHoveredFlags.ChildWindows)) handle.HoldOpen();
                var background = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
                background.W *= LauncherDock.ClampPercent(current.BackgroundOpacity, 0, 100, 100) / 100;
                ImGui.GetWindowDrawList().AddRectFilled(dock.Position, dock.Position + dock.Size,
                    ImGui.GetColorU32(background), ImGui.GetStyle().WindowRounding);
            }
            handle.Draw(dock, current.HandlePosition, move, save, toggleDock);
            if (!expanded) return;

            ImGui.SetCursorScreenPos(dock.Position + dock.ButtonsPosition);
            var available = dock.Size - dock.ButtonsPosition - new Vector2(6 * ImGuiHelpers.GlobalScale);
            if (current.Docked && current.Edge == LauncherEdge.Bottom) available.Y -= dock.GripSize.Y;
            if (current.Docked && current.Edge == LauncherEdge.Right) available.X -= dock.GripSize.X;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            try
            {
                if (ImGui.BeginChild(contentId, Vector2.Max(Vector2.One, available), false)) draw(rows);
                ImGui.EndChild();
            }
            finally { ImGui.PopStyleColor(); }
        }
        finally { ImGui.PopStyleVar(); }
    }
}
