using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

public sealed class LauncherWindow : Window
{
    private readonly Action openMain, openTrade, toggleInterfaces;
    private readonly Func<int> requests, announcements;
    private readonly Func<bool> canDraw, interfacesVisible;

    public LauncherWindow(Action openMain, Action openTrade, Func<int> requests, Func<int> announcements,
        Func<bool> canDraw, Func<bool> interfacesVisible, Action toggleInterfaces)
        : base("Amor RP launcher###AmorRPLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.openMain = openMain; this.openTrade = openTrade; this.requests = requests;
        this.canDraw = canDraw; this.announcements = announcements;
        this.interfacesVisible = interfacesVisible; this.toggleInterfaces = toggleInterfaces;
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
        ImGui.SameLine();
        var visible = interfacesVisible();
        if (visible) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.35f, .29f, .17f, 1));
        ImGui.PushID("LauncherInterfaces");
        if (ImGuiComponents.IconButtonWithText(visible ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash,
                visible ? "Hide panels" : "Show panels", new Vector2(0, size.Y))) toggleInterfaces();
        ImGui.PopID();
        if (visible) ImGui.PopStyleColor();
    }
}
