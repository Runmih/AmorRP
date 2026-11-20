using System.Numerics;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

public sealed class LauncherWindow : Window
{
    private readonly Action toggleWriting, toggleTrade, toggleInterfaces;
    private readonly Action<WorkspaceKind> toggleWorkspace;
    private readonly Func<WorkspaceKind, bool> workspaceVisible;
    private readonly Func<int> requests, announcements;
    private readonly Func<bool> canDraw, interfacesVisible, canManageGroups, canManageCommunity, writingVisible, tradeVisible;

    private readonly Configuration configuration;
    private readonly Action save;
    private LauncherDock dock;
    private bool expanded, pinned, dragging, showGroups, showAdmin, lastAutoHide;
    private float dragStart;
    private double visibleUntil;
    private LauncherEdge lastEdge;

    public LauncherWindow(Configuration configuration, Action save, Action toggleWriting, Action toggleTrade, Action<WorkspaceKind> toggleWorkspace,
        Func<bool> writingVisible, Func<bool> tradeVisible, Func<WorkspaceKind, bool> workspaceVisible,
        Func<int> requests, Func<int> announcements, Func<bool> canDraw, Func<bool> interfacesVisible,
        Action toggleInterfaces, Func<bool> canManageGroups, Func<bool> canManageCommunity)
        : base("Amor RP launcher###AmorRPLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNavFocus)
    {
        this.configuration = configuration;
        this.save = save;
        this.toggleWriting = toggleWriting; this.toggleTrade = toggleTrade; this.requests = requests;
        this.canDraw = canDraw; this.toggleWorkspace = toggleWorkspace; this.announcements = announcements;
        this.writingVisible = writingVisible; this.tradeVisible = tradeVisible; this.workspaceVisible = workspaceVisible;
        this.canManageGroups = canManageGroups; this.canManageCommunity = canManageCommunity;
        this.interfacesVisible = interfacesVisible; this.toggleInterfaces = toggleInterfaces;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override bool DrawConditions() => canDraw();

    public override void PreDraw()
    {
        var edge = configuration.QuickBarEdge;
        var autoHide = configuration.QuickBarAutoHide;
        if (edge != lastEdge || autoHide != lastAutoHide)
        {
            pinned = false;
            visibleUntil = 0;
            lastEdge = edge;
            lastAutoHide = autoHide;
        }
        showGroups = canManageGroups();
        showAdmin = canManageCommunity();
        expanded = !autoHide || pinned || dragging || ImGui.GetTime() < visibleUntil;
        var viewport = ImGui.GetMainViewport();
        var buttonSize = ImGui.GetFrameHeight() * 1.4f * LauncherDock.ClampPercent(configuration.QuickBarSize, 60, 160, 100) / 100;
        dock = LauncherDock.Place(edge, viewport.WorkPos, viewport.WorkSize, configuration.QuickBarPosition,
            buttonSize, ImGuiHelpers.GlobalScale, 6 + (showGroups ? 1 : 0) + (showAdmin ? 1 : 0), expanded);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.SetNextWindowPos(dock.Position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(dock.Size, ImGuiCond.Always);
    }

    public override void PostDraw() => ImGui.PopStyleVar(2);

    public override void Draw()
    {
        var opacity = LauncherDock.ClampPercent(configuration.QuickBarOpacity, 25, 100, 100) / 100;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * opacity);
        try
        {
            if (expanded)
            {
                if (ImGui.IsWindowHovered()) KeepVisible();
                ImGui.GetWindowDrawList().AddRectFilled(dock.Position, dock.Position + dock.Size,
                    ImGui.GetColorU32(ImGuiCol.WindowBg), ImGui.GetStyle().WindowRounding);
            }
            DrawHandle();
            if (expanded) DrawButtons();
        }
        finally { ImGui.PopStyleVar(); }
    }

    private void KeepVisible() => visibleUntil = ImGui.GetTime() + .35;

    private void DrawHandle()
    {
        ImGui.SetCursorScreenPos(dock.Position + dock.GripPosition);
        var clicked = ImGui.InvisibleButton("##LauncherDockHandle", dock.GripSize);
        var hovered = ImGui.IsItemHovered();
        if (hovered || ImGui.IsItemActive()) KeepVisible();
        if (ImGui.IsItemActivated()) dragStart = LauncherDock.ClampPercent(configuration.QuickBarPosition, 0, 100, 50);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            dragging = true;
            var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left);
            var movement = LauncherDock.IsHorizontal(lastEdge) ? delta.X : delta.Y;
            if (dock.Travel > 0) configuration.QuickBarPosition = Math.Clamp(dragStart + 100 * movement / dock.Travel, 0, 100);
        }
        if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
            save();
        }
        else if (clicked && configuration.QuickBarAutoHide)
        {
            pinned = !pinned;
        }

        var center = dock.Position + dock.GripPosition + dock.GripSize / 2;
        var half = dock.GripSize * (LauncherDock.IsHorizontal(lastEdge) ? new Vector2(.36f, .12f) : new Vector2(.12f, .36f));
        var tint = PluginTheme.Accent;
        tint.W = hovered || dragging || pinned ? 1 : .62f;
        ImGui.GetWindowDrawList().AddRectFilled(center - half, center + half, ImGui.GetColorU32(tint), 2);
        if (hovered && !dragging)
            ImGui.SetTooltip(configuration.QuickBarAutoHide ? "Drag to move. Click to pin or unpin." : "Drag to move the quick bar.");
    }

    public override void OnClose()
    {
        pinned = false;
        visibleUntil = 0;
        if (!dragging) return;
        dragging = false;
        save();
    }

    private void DrawButtons()
    {
        ImGui.SetCursorScreenPos(dock.Position + dock.ButtonsPosition);
        var size = new Vector2(dock.ButtonSize);
        void Next()
        {
            var end = ImGui.GetItemRectMax();
            var start = dock.Position + dock.ButtonsPosition;
            ImGui.SetCursorScreenPos(LauncherDock.IsHorizontal(lastEdge)
                ? new(end.X + dock.Gap, start.Y)
                : new(start.X, end.Y + dock.Gap));
        }
        DrawWorkspaceButton("LauncherCommunity", FontAwesomeIcon.Home, size, WorkspaceKind.Season, announcements() > 0);
        Next();
        DrawWorkspaceButton("LauncherInventory", FontAwesomeIcon.BoxOpen, size, WorkspaceKind.Inventory);
        Next();
        DrawWorkspaceButton("LauncherCrafting", FontAwesomeIcon.Hammer, size, WorkspaceKind.Crafting);
        Next();
        if (ToggleButton("LauncherWriting", FontAwesomeIcon.BookOpen, size, writingVisible(), "Writing log")) toggleWriting();
        if (showGroups)
        {
            Next();
            DrawWorkspaceButton("LauncherGroupManagement", FontAwesomeIcon.Users, size, WorkspaceKind.GroupManagement);
        }
        Next();
        if (ToggleButton("LauncherTrade", FontAwesomeIcon.BalanceScale, size, tradeVisible(), "Trade", requests() > 0)) toggleTrade();
        if (showAdmin)
        {
            Next();
            DrawWorkspaceButton("LauncherCommunityManagement", FontAwesomeIcon.Key, size, WorkspaceKind.CommunityManagement);
        }
        Next();
        if (ToggleButton("##LauncherInterfaces", FontAwesomeIcon.PowerOff, size, interfacesVisible(), "automatic HUDs", power: true)) toggleInterfaces();
    }

    private void DrawWorkspaceButton(string id, FontAwesomeIcon icon, Vector2 size, WorkspaceKind kind, bool badge = false)
    {
        if (ToggleButton(id, icon, size, workspaceVisible(kind), WorkspaceWindow.Title(kind), badge)) toggleWorkspace(kind);
    }

    private static bool ToggleButton(string id, FontAwesomeIcon icon, Vector2 size, bool active, string label, bool badge = false, bool power = false)
    {
        if (active) ImGui.PushStyleColor(ImGuiCol.Button, PluginTheme.SelectedButton);
        bool clicked;
        ImGui.PushFont(UiBuilder.IconFont);
        try { clicked = ImGui.Button(icon.ToIconString() + "###" + id, size); }
        finally
        {
            ImGui.PopFont();
            if (active) ImGui.PopStyleColor();
        }
        if (badge) NotificationBadge.Draw();
        if (ImGui.IsItemHovered())
        {
            var action = power ? (active ? "Turn off " : "Turn on ") : (active ? "Close " : "Open ");
            ImGui.SetTooltip(action + label);
        }
        return clicked;
    }
}
