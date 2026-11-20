using AmorRP.Plugin.Features.Shell;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private string? connectedServerVersion;
    private bool requestedAccountSettings;
    private static string PluginVersion => typeof(SeasonsPanel).Assembly.GetName().Version?.ToString() ?? "Unknown";

    private void DrawSettingsWindow(bool busy, string localName, string localWorld, uint worldId)
    {
        var pending = configuration.PendingCommands.FirstOrDefault(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session?.Character.Id);
        ImGui.TextColored(InventoryGold, "SETTINGS");
        var footerHeight = ActionFeedbackHeight(pending);
        ImGui.BeginChild("SettingsContent", new(0, footerHeight > 0 ? -footerHeight - ImGui.GetStyle().ItemSpacing.Y : 0), false);
        if (ImGui.BeginTabBar("PluginSettingsTabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem("Interface")) { DrawInterfaceSettings(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Account", requestedAccountSettings ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                requestedAccountSettings = false;
                ImGui.Spacing();
                DrawLogin(busy, localName, localWorld, worldId);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("About")) { DrawAboutSettings(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
        ImGui.EndChild(); DrawActionFeedback(pending); DrawSharedDialogsOnce();
    }

    private static readonly string[] QuickBarEdges = ["Top · Horizontal", "Bottom · Horizontal", "Left · Vertical", "Right · Vertical"];

    private void DrawQuickBarSettings()
    {
        var edge = (int)configuration.QuickBarEdge;
        if (ImGui.Combo("Dock edge", ref edge, QuickBarEdges, QuickBarEdges.Length))
        {
            configuration.QuickBarEdge = (LauncherEdge)edge;
            save();
        }

        var autoHide = configuration.QuickBarAutoHide;
        if (ImGui.Checkbox("Auto-hide quick bar", ref autoHide))
        {
            configuration.QuickBarAutoHide = autoHide;
            save();
        }

        var size = LauncherDock.ClampPercent(configuration.QuickBarSize, 60, 160, 100);
        if (ImGui.SliderFloat("Size##QuickBar", ref size, 60, 160, "%.0f%%"))
            configuration.QuickBarSize = size;
        if (ImGui.IsItemDeactivatedAfterEdit()) save();

        var opacity = LauncherDock.ClampPercent(configuration.QuickBarOpacity, 25, 100, 100);
        if (ImGui.SliderFloat("Opacity##QuickBar", ref opacity, 25, 100, "%.0f%%"))
            configuration.QuickBarOpacity = opacity;
        if (ImGui.IsItemDeactivatedAfterEdit()) save();

        var horizontal = LauncherDock.IsHorizontal(configuration.QuickBarEdge);
        var position = LauncherDock.ClampPercent(configuration.QuickBarPosition, 0, 100, 50);
        if (ImGui.SliderFloat(horizontal ? "Position (left to right)##QuickBar" : "Position (top to bottom)##QuickBar",
                ref position, 0, 100, "%.0f%%"))
            configuration.QuickBarPosition = position;
        if (ImGui.IsItemDeactivatedAfterEdit()) save();

        Wrapped(autoHide ? "Hover the handle to reveal the quick bar. Drag it to move the bar along the edge."
            : "Drag the handle to move the quick bar along the edge.");
    }

    private void DrawInterfaceSettings()
    {
        ImGui.Spacing();
        var show = configuration.ShowLauncher;
        if (ImGui.Checkbox("Show quick launcher", ref show))
        { configuration.ShowLauncher = show; save(); SetLauncherVisible?.Invoke(show); }
        DrawQuickBarSettings();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        var attached = configuration.ShowAttachedInterfaces;
        if (ImGui.Checkbox("Enable automatic HUDs", ref attached))
        { configuration.ShowAttachedInterfaces = attached; save(); }
        Wrapped("The launcher's power button controls this switch. Your HUD preference is remembered.");
        ImGui.Spacing();
        var events = configuration.ShowFavoriteEvents;
        if (ImGui.Checkbox("Favorite events HUD", ref events))
        { configuration.ShowFavoriteEvents = events; save(); SetFavoriteEventsVisible?.Invoke(events); }
        Wrapped("Favorite events cover the next five days. Drag the HUD header to move it; click an event for details.");
    }

    private void DrawAboutSettings()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted("Amor RP " + PluginVersion);
        ImGui.TextUnformatted("Server version (last connected): " + (connectedServerVersion ?? "Not connected"));
        if (connectedServerVersion != null && session != null)
            ImGui.TextDisabled("Seasons");
        ImGui.Spacing();
        Wrapped("Settings: /amorrp settings");
        Wrapped("Windows: /amorrp season (Home), /amorrp inventory, /amorrp crafting, /amorrp writing, /amorrp groups, /amorrp manage and /amorrp trade");
        ImGui.Spacing();
        if (ImGui.Button("Copy version info"))
            ImGui.SetClipboardText($"Amor RP plugin: {PluginVersion}\nServer (last connected): {connectedServerVersion ?? "Not connected"}");
    }
}
