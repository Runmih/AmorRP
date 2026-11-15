using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

public sealed class LauncherWindow : Window
{
    private readonly Action openWriting, openTrade, toggleInterfaces;
    private readonly Action<WorkspaceKind> openWorkspace;
    private readonly Func<int> requests, announcements;
    private readonly Func<bool> canDraw, interfacesVisible, canManageGroups, canManageCommunity;

    public LauncherWindow(Action openWriting, Action openTrade, Action<WorkspaceKind> openWorkspace,
        Func<int> requests, Func<int> announcements, Func<bool> canDraw, Func<bool> interfacesVisible,
        Action toggleInterfaces, Func<bool> canManageGroups, Func<bool> canManageCommunity)
        : base("Amor RP launcher###AmorRPLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.openWriting = openWriting; this.openTrade = openTrade; this.requests = requests;
        this.canDraw = canDraw;
        this.openWorkspace = openWorkspace; this.announcements = announcements;
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
        if (ImGuiComponents.IconButton("LauncherWriting", FontAwesomeIcon.BookOpen, size)) openWriting();
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("LauncherCrafting", FontAwesomeIcon.Hammer, size)) openWorkspace(WorkspaceKind.Crafting);
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("LauncherCommunity", FontAwesomeIcon.Users, size)) openWorkspace(WorkspaceKind.Season);
        if (announcements() > 0) NotificationBadge.Draw();
        if (canManageGroups())
        {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton("LauncherGroupManagement", FontAwesomeIcon.Sitemap, size)) openWorkspace(WorkspaceKind.GroupManagement);
        }
        if (canManageCommunity())
        {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton("LauncherCommunityManagement", FontAwesomeIcon.Cog, size)) openWorkspace(WorkspaceKind.CommunityManagement);
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("LauncherTrade", FontAwesomeIcon.ExchangeAlt, size)) openTrade();
        if (requests() > 0) NotificationBadge.Draw();
        ImGui.SameLine();
        var visible = interfacesVisible();
        if (visible) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.35f, .29f, .17f, 1));
        if (ImGui.Button("##LauncherInterfaces", size)) toggleInterfaces();
        DrawClosedEye();
        if (visible) ImGui.PopStyleColor();
    }

    private static void DrawClosedEye()
    {
        var origin = ImGui.GetItemRectMin(); var size = ImGui.GetItemRectSize();
        Vector2 Point(float x, float y) => origin + new Vector2(size.X * x, size.Y * y);
        var draw = ImGui.GetWindowDrawList(); var color = ImGui.GetColorU32(ImGuiCol.Text);
        var thickness = Math.Max(1.3f, size.X * .05f);
        draw.AddBezierCubic(Point(.22f, .40f), Point(.36f, .65f), Point(.64f, .65f), Point(.78f, .40f), color, thickness, 16);
        draw.AddLine(Point(.35f, .54f), Point(.27f, .69f), color, thickness);
        draw.AddLine(Point(.50f, .59f), Point(.50f, .77f), color, thickness);
        draw.AddLine(Point(.65f, .54f), Point(.73f, .69f), color, thickness);
    }
}
