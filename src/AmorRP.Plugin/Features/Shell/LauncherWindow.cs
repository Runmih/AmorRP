using System.Numerics;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

public sealed class LauncherWindow : Window
{
    private readonly Action toggleWriting, toggleTrade, toggleInterfaces;
    private readonly Action<WorkspaceKind> toggleWorkspace;
    private readonly Func<WorkspaceKind, bool> workspaceVisible;
    private readonly Func<int> requests, announcements;
    private readonly Func<bool> canDraw, interfacesVisible, canManageGroups, canManageCommunity, writingVisible, tradeVisible;

    public LauncherWindow(Action toggleWriting, Action toggleTrade, Action<WorkspaceKind> toggleWorkspace,
        Func<bool> writingVisible, Func<bool> tradeVisible, Func<WorkspaceKind, bool> workspaceVisible,
        Func<int> requests, Func<int> announcements, Func<bool> canDraw, Func<bool> interfacesVisible,
        Action toggleInterfaces, Func<bool> canManageGroups, Func<bool> canManageCommunity)
        : base("Amor RP launcher###AmorRPLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.toggleWriting = toggleWriting; this.toggleTrade = toggleTrade; this.requests = requests;
        this.canDraw = canDraw; this.toggleWorkspace = toggleWorkspace; this.announcements = announcements;
        this.writingVisible = writingVisible; this.tradeVisible = tradeVisible; this.workspaceVisible = workspaceVisible;
        this.canManageGroups = canManageGroups; this.canManageCommunity = canManageCommunity;
        this.interfacesVisible = interfacesVisible; this.toggleInterfaces = toggleInterfaces;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Position = new Vector2(24, 180); PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions() => canDraw();

    public override void Draw()
    {
        var size = new Vector2(ImGui.GetFrameHeight() * 1.4f);
        DrawWorkspaceButton("LauncherCommunity", FontAwesomeIcon.Home, size, WorkspaceKind.Season, announcements() > 0);
        ImGui.SameLine();
        DrawWorkspaceButton("LauncherInventory", FontAwesomeIcon.BoxOpen, size, WorkspaceKind.Inventory);
        ImGui.SameLine();
        DrawWorkspaceButton("LauncherCrafting", FontAwesomeIcon.Hammer, size, WorkspaceKind.Crafting);
        ImGui.SameLine();
        if (ToggleButton("LauncherWriting", FontAwesomeIcon.BookOpen, size, writingVisible(), "Writing log")) toggleWriting();
        if (canManageGroups())
        {
            ImGui.SameLine();
            DrawWorkspaceButton("LauncherGroupManagement", FontAwesomeIcon.Users, size, WorkspaceKind.GroupManagement);
        }
        ImGui.SameLine();
        if (ToggleButton("LauncherTrade", FontAwesomeIcon.BalanceScale, size, tradeVisible(), "Trade", requests() > 0)) toggleTrade();
        if (canManageCommunity())
        {
            ImGui.SameLine();
            DrawWorkspaceButton("LauncherCommunityManagement", FontAwesomeIcon.Key, size, WorkspaceKind.CommunityManagement);
        }
        ImGui.SameLine();
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
        try { clicked = ImGuiComponents.IconButton(id, icon, size); }
        finally { if (active) ImGui.PopStyleColor(); }
        if (badge) NotificationBadge.Draw();
        if (ImGui.IsItemHovered())
        {
            var action = power ? (active ? "Turn off " : "Turn on ") : (active ? "Close " : "Open ");
            ImGui.SetTooltip(action + label);
        }
        return clicked;
    }
}
