using System.Numerics;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Shell;

internal sealed class PanelHandle
{
    private bool pinned, dragging;
    private double visibleUntil;
    private Vector2 dragStart;
    private (bool Docked, LauncherEdge Edge, bool AutoHide) placement;

    public bool IsExpanded(bool docked, LauncherEdge edge, bool autoHide)
    {
        var next = (docked, edge, autoHide);
        if (next != placement)
        {
            pinned = dragging = false;
            visibleUntil = 0;
            placement = next;
        }
        return !docked || !autoHide || pinned || dragging || ImGui.GetTime() < visibleUntil;
    }

    public void HoldOpen() => visibleUntil = ImGui.GetTime() + .35;

    public void Draw(LauncherDock dock, Vector2 position, Action<Vector2> move, Action save,
        Action toggleDock, Action? openSettings = null, Action? openLog = null, string? status = null)
    {
        ImGui.SetCursorScreenPos(dock.Position + dock.GripPosition);
        var clicked = ImGui.InvisibleButton("##LauncherDockHandle", dock.GripSize);
        var hovered = ImGui.IsItemHovered();
        if (hovered || ImGui.IsItemActive()) HoldOpen();
        if (ImGui.IsItemActivated())
            dragStart = new(LauncherDock.ClampPercent(position.X, 0, 100, 50), LauncherDock.ClampPercent(position.Y, 0, 100, 50));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            dragging = true;
            var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left);
            var next = dragStart;
            if (!placement.Docked)
            {
                if (dock.Travel > 0) next.X += 100 * delta.X / dock.Travel;
                if (dock.VerticalTravel > 0) next.Y += 100 * delta.Y / dock.VerticalTravel;
            }
            else if (dock.Travel > 0)
            {
                var along = LauncherDock.IsHorizontal(placement.Edge) ? delta.X : delta.Y;
                next.X += 100 * along / dock.Travel;
            }
            move(Vector2.Clamp(next, Vector2.Zero, new Vector2(100)));
        }

        if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
            save();
        }
        else if (clicked && placement.Docked && placement.AutoHide)
            pinned = !pinned;

        var center = dock.Position + dock.GripPosition + dock.GripSize / 2;
        var horizontal = !placement.Docked || LauncherDock.IsHorizontal(placement.Edge);
        var half = dock.GripSize * (horizontal ? new Vector2(.36f, .12f) : new Vector2(.12f, .36f));
        var tint = PluginTheme.Accent;
        tint.W = hovered || dragging || pinned ? 1 : .62f;
        ImGui.GetWindowDrawList().AddRectFilled(center - half, center + half, ImGui.GetColorU32(tint), 2);
        if (ImGui.BeginPopupContextItem("##PanelOptions"))
        {
            HoldOpen();
            if (ImGui.MenuItem(placement.Docked ? "Undock" : "Dock")) { toggleDock(); save(); }
            if (openSettings != null && ImGui.MenuItem("Settings")) openSettings();
            if (openLog != null && ImGui.MenuItem("Activity Log")) openLog();
            ImGui.EndPopup();
        }
        else if (hovered && !dragging)
            ImGui.SetTooltip((placement.Docked && placement.AutoHide
                ? "Drag to move. Click to pin. Right-click for options."
                : "Drag to move. Right-click for options.") + (status == null ? "" : "\n" + status));
    }

    public void Closed(Action save)
    {
        if (dragging) save();
        pinned = dragging = false;
        visibleUntil = 0;
    }
}
