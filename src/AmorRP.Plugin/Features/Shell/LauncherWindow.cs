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
    private readonly Func<bool> canDraw, interfacesVisible, canManageCommunity, writingVisible, tradeVisible;

    private readonly Configuration configuration;
    private readonly Action save;
    private LauncherDock dock;
    private readonly PanelHandle handle = new();
    private readonly Action openSettings, openLog;
    private bool expanded, showAdmin;

    public LauncherWindow(Configuration configuration, Action save, Action openSettings, Action openLog, Action toggleWriting, Action toggleTrade, Action<WorkspaceKind> toggleWorkspace,
        Func<bool> writingVisible, Func<bool> tradeVisible, Func<WorkspaceKind, bool> workspaceVisible,
        Func<int> requests, Func<int> announcements, Func<bool> canDraw, Func<bool> interfacesVisible,
        Action toggleInterfaces, Func<bool> canManageCommunity)
        : base("Amor RP launcher###AmorRPLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNavFocus)
    {
        this.configuration = configuration;
        this.save = save;
        this.openSettings = openSettings;
        this.openLog = openLog;
        this.toggleWriting = toggleWriting; this.toggleTrade = toggleTrade; this.requests = requests;
        this.canDraw = canDraw; this.toggleWorkspace = toggleWorkspace; this.announcements = announcements;
        this.writingVisible = writingVisible; this.tradeVisible = tradeVisible; this.workspaceVisible = workspaceVisible;
        this.canManageCommunity = canManageCommunity;
        this.interfacesVisible = interfacesVisible; this.toggleInterfaces = toggleInterfaces;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override bool DrawConditions() => canDraw();

    public override void PreDraw()
    {
        var edge = configuration.QuickBarEdge;
        showAdmin = canManageCommunity();
        expanded = handle.IsExpanded(configuration.QuickBarDocked, edge, configuration.QuickBarAutoHide);
        var viewport = ImGui.GetMainViewport();
        var buttonSize = ImGui.GetFrameHeight() * 1.4f * LauncherDock.ClampPercent(configuration.QuickBarSize, 60, 160, 100) / 100;
        dock = LauncherDock.Place(edge, viewport.WorkPos, viewport.WorkSize, configuration.QuickBarPosition,
            buttonSize, ImGuiHelpers.GlobalScale, 8 + (showAdmin ? 1 : 0), expanded,
            configuration.QuickBarDocked, new Vector2(configuration.QuickBarFloatX, configuration.QuickBarFloatY));
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
            var tradeCount = requests();
            var announcementCount = announcements();
            if (expanded)
            {
                if (ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup)) handle.HoldOpen();
                ImGui.GetWindowDrawList().AddRectFilled(dock.Position, dock.Position + dock.Size,
                    ImGui.GetColorU32(ImGuiCol.WindowBg), ImGui.GetStyle().WindowRounding);
            }
            DrawHandle(tradeCount, announcementCount);
            if (expanded) DrawButtons(tradeCount > 0, announcementCount > 0);
            else NotificationBadge.DrawOnHandle(dock, LauncherDock.IsHorizontal(configuration.QuickBarEdge),
                tradeCount > 0, announcementCount > 0);
        }
        finally { ImGui.PopStyleVar(); }
    }

    private void DrawHandle(int tradeCount, int announcementCount)
    {
        var docked = configuration.QuickBarDocked;
        var position = docked ? new Vector2(configuration.QuickBarPosition, 0)
            : new Vector2(configuration.QuickBarFloatX, configuration.QuickBarFloatY);
        var status = (tradeCount > 0, announcementCount > 0) switch
        {
            (true, true) => $"Red · Trade requests ({tradeCount})\nBlue · Unread announcements ({announcementCount})",
            (true, false) => $"Red · Trade requests ({tradeCount})",
            (false, true) => $"Blue · Unread announcements ({announcementCount})",
            _ => null
        };
        handle.Draw(dock, position, next =>
        {
            if (docked) configuration.QuickBarPosition = next.X;
            else
            {
                configuration.QuickBarFloatX = next.X;
                configuration.QuickBarFloatY = next.Y;
            }
        }, save, () => configuration.QuickBarDocked = !configuration.QuickBarDocked, openSettings, openLog, status);
    }

    public override void OnClose() => handle.Closed(save);

    private void DrawButtons(bool hasTrade, bool hasAnnouncements)
    {
        ImGui.SetCursorScreenPos(dock.Position + dock.ButtonsPosition);
        var size = new Vector2(dock.ButtonSize);
        void Next()
        {
            var end = ImGui.GetItemRectMax();
            var start = dock.Position + dock.ButtonsPosition;
            ImGui.SetCursorScreenPos(LauncherDock.IsHorizontal(configuration.QuickBarEdge)
                ? new(end.X + dock.Gap, start.Y)
                : new(start.X, end.Y + dock.Gap));
        }
        DrawWorkspaceButton("LauncherCommunity", FontAwesomeIcon.Home, size, WorkspaceKind.Season, hasAnnouncements);
        Next();
        DrawWorkspaceButton("LauncherInventory", FontAwesomeIcon.BoxOpen, size, WorkspaceKind.Inventory);
        Next();
        DrawWorkspaceButton("LauncherCrafting", FontAwesomeIcon.Hammer, size, WorkspaceKind.Crafting);
        Next();
        if (ToggleButton("LauncherWriting", FontAwesomeIcon.BookOpen, size, writingVisible(), "Writing log")) toggleWriting();
        Next();
        DrawWorkspaceButton("LauncherGroupManagement", FontAwesomeIcon.Flag, size, WorkspaceKind.Groups);
        Next();
        DrawWorkspaceButton("LauncherCombatActions", FontAwesomeIcon.Khanda, size, WorkspaceKind.CombatActions);
        Next();
        if (ToggleButton("LauncherTrade", FontAwesomeIcon.BalanceScale, size, tradeVisible(), "Trade", hasTrade)) toggleTrade();
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
        if (ToggleButton(id, icon, size, workspaceVisible(kind), WorkspaceWindow.Title(kind), badge, announcement: kind == WorkspaceKind.Season)) toggleWorkspace(kind);
    }

    private static bool ToggleButton(string id, FontAwesomeIcon icon, Vector2 size, bool active, string label, bool badge = false, bool power = false, bool announcement = false)
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
        if (badge) NotificationBadge.Draw(announcement);
        if (ImGui.IsItemHovered())
        {
            var action = power ? (active ? "Turn off " : "Turn on ") : (active ? "Close " : "Open ");
            ImGui.SetTooltip(action + label);
        }
        return clicked;
    }
}
