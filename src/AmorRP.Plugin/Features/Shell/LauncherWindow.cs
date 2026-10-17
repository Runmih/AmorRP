using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

public sealed class LauncherWindow : Window
{
    private readonly Action openMain, openTrade, drawCommunities;
    private readonly Func<int> requests, announcements;
    private readonly Func<bool> canDraw;

    public LauncherWindow(Action openMain, Action openTrade, Func<int> requests, Func<int> announcements, Func<bool> canDraw, Action drawCommunities)
        : base("Amor RP launcher###AmorRPLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.openMain = openMain; this.openTrade = openTrade; this.requests = requests;
        this.canDraw = canDraw; this.announcements = announcements; this.drawCommunities = drawCommunities;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Position = new Vector2(24, 180); PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions() => canDraw();

    public override void Draw()
    {
        var size = new Vector2(ImGui.GetFrameHeight() * 1.4f);
        if (ImGuiComponents.IconButton("LauncherMain", FontAwesomeIcon.BookOpen, size)) openMain();
        if (announcements() > 0) NotificationBadge.Draw();
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("LauncherTrade", FontAwesomeIcon.ExchangeAlt, size)) openTrade();
        if (requests() > 0) NotificationBadge.Draw();
        ImGui.SameLine(); drawCommunities();
    }
}
