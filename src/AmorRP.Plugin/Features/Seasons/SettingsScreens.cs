using AmorRP.Plugin.Features.Shell;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private string? connectedServerVersion;
    private bool requestedAccountSettings;
    internal Action? CheckForPluginUpdates { get; set; }
    private static string PluginVersion => typeof(SeasonsPanel).Assembly.GetName().Version?.ToString() ?? "Unknown";

    private void DrawSettingsWindow(bool busy, string localName, string localWorld, uint worldId)
    {
        ImGui.TextColored(InventoryGold, "SETTINGS");
        ImGui.BeginChild("SettingsContent", System.Numerics.Vector2.Zero, false);
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
        ImGui.EndChild(); DrawSharedDialogsOnce();
    }

    private static readonly string[] QuickBarEdges = ["Top · Horizontal", "Bottom · Horizontal", "Left · Vertical", "Right · Vertical"];

    private static readonly string[] HudEdges = ["Top", "Bottom", "Left", "Right"];

    private void DrawDockSettings(string[] edges, bool docked, Action<bool> setDocked, LauncherEdge edge, Action<LauncherEdge> setEdge,
        bool autoHide, Action<bool> setAutoHide, float position, Action<float> setPosition)
    {
        if (ImGui.Checkbox("Dock to edge", ref docked)) { setDocked(docked); save(); }
        var index = (int)edge;
        if (docked && ImGui.Combo("Dock edge", ref index, edges, edges.Length))
        {
            edge = (LauncherEdge)index;
            setEdge(edge);
            save();
        }
        if (docked)
        {
            if (ImGui.Checkbox("Auto-hide", ref autoHide)) { setAutoHide(autoHide); save(); }
            PercentSlider("Position along edge", position, 0, 100, 50, setPosition);
        }
        Wrapped(docked && autoHide ? "Hover to reveal. Drag the handle to move along the edge."
            : "Drag the handle to move the panel.");
    }

    private void PercentSlider(string name, float value, float minimum, float maximum, float fallback, Action<float> update)
    {
        value = LauncherDock.ClampPercent(value, minimum, maximum, fallback);
        if (ImGui.SliderFloat(name, ref value, minimum, maximum, "%.0f%%")) update(value);
        if (ImGui.IsItemDeactivatedAfterEdit()) save();
    }

    private void DrawInterfaceSettings()
    {
        if (ImGui.CollapsingHeader("Quick bar")) DrawQuickBarSettings();

        if (ImGui.CollapsingHeader("Main window"))
        {
            var navigation = (int)configuration.HubNavigationEdge;
            if (ImGui.Combo("Navigation position", ref navigation, HudEdges, HudEdges.Length))
            {
                configuration.HubNavigationEdge = (LauncherEdge)navigation;
                save();
            }
        }

        if (ImGui.CollapsingHeader("Automatic HUDs"))
        {
            var attached = configuration.ShowAttachedInterfaces;
            if (ImGui.Checkbox("Enable automatic HUDs", ref attached))
            { configuration.ShowAttachedInterfaces = attached; save(); }
            Wrapped("The quick bar's Power button also controls automatic HUDs.");
        }

        if (ImGui.CollapsingHeader("Favorite Events")) DrawFavoriteEventsSettings();
        if (ImGui.CollapsingHeader("Coins"))
            DrawHudSettings("CoinHud", "Show coins", "Hide when the balance is unavailable", configuration.CoinHud, SetCoinHudVisible);
        if (ImGui.CollapsingHeader("Health"))
            DrawHudSettings("HealthHud", "Show health", "Hide when health is unavailable", configuration.HealthHud, SetHealthHudVisible);
    }

    private void DrawQuickBarSettings()
    {
        ImGui.PushID("QuickBar");
        var show = configuration.ShowLauncher;
        if (ImGui.Checkbox("Show quick bar", ref show))
        { configuration.ShowLauncher = show; save(); SetLauncherVisible?.Invoke(show); }
        DrawDockSettings(QuickBarEdges, configuration.QuickBarDocked, x => configuration.QuickBarDocked = x,
            configuration.QuickBarEdge, x => configuration.QuickBarEdge = x,
            configuration.QuickBarAutoHide, x => configuration.QuickBarAutoHide = x,
            configuration.QuickBarPosition, x => configuration.QuickBarPosition = x);
        if (!configuration.QuickBarDocked)
        {
            var orientation = LauncherDock.IsHorizontal(configuration.QuickBarEdge) ? 0 : 1;
            if (ImGui.Combo("Orientation", ref orientation, ["Horizontal", "Vertical"], 2))
            {
                configuration.QuickBarEdge = orientation == 0 ? LauncherEdge.Top : LauncherEdge.Left;
                save();
            }
        }
        PercentSlider("Size", configuration.QuickBarSize, 60, 160, 100, x => configuration.QuickBarSize = x);
        PercentSlider("Opacity", configuration.QuickBarOpacity, 25, 100, 100, x => configuration.QuickBarOpacity = x);
        ImGui.PopID();
    }

    private void DrawFavoriteEventsSettings()
    {
        ImGui.PushID("FavoriteEvents");
        var events = configuration.ShowFavoriteEvents;
        if (ImGui.Checkbox("Show Favorite Events", ref events))
        { configuration.ShowFavoriteEvents = events; save(); SetFavoriteEventsVisible?.Invoke(events); }
        var header = configuration.FavoriteEventsShowHeader;
        if (ImGui.Checkbox("Show header", ref header)) { configuration.FavoriteEventsShowHeader = header; save(); }
        var hideEmpty = configuration.FavoriteEventsHideWhenEmpty;
        if (ImGui.Checkbox("Hide when there are no events", ref hideEmpty)) { configuration.FavoriteEventsHideWhenEmpty = hideEmpty; save(); }
        DrawDockSettings(HudEdges, configuration.FavoriteEventsDocked, x => configuration.FavoriteEventsDocked = x,
            configuration.FavoriteEventsEdge, x => configuration.FavoriteEventsEdge = x,
            configuration.FavoriteEventsAutoHide, x => configuration.FavoriteEventsAutoHide = x,
            configuration.FavoriteEventsPosition, x => configuration.FavoriteEventsPosition = x);
        PercentSlider("Width", configuration.FavoriteEventsSize, 60, 160, 100, x => configuration.FavoriteEventsSize = x);
        PercentSlider("Opacity", configuration.FavoriteEventsOpacity, 25, 100, 100, x => configuration.FavoriteEventsOpacity = x);
        PercentSlider("Background opacity", configuration.FavoriteEventsBackgroundOpacity, 0, 100, 100, x => configuration.FavoriteEventsBackgroundOpacity = x);
        ImGui.PopID();
    }

    private void DrawHudSettings(string id, string visibleLabel, string emptyLabel, CoinHudSettings panel, Action<bool>? setVisible)
    {
        ImGui.PushID(id);
        var show = panel.Visible;
        if (ImGui.Checkbox(visibleLabel, ref show))
        { panel.Visible = show; save(); setVisible?.Invoke(show); }
        var header = panel.ShowHeader;
        if (ImGui.Checkbox("Show header", ref header)) { panel.ShowHeader = header; save(); }
        var hideEmpty = panel.HideWhenEmpty;
        if (ImGui.Checkbox(emptyLabel, ref hideEmpty))
        { panel.HideWhenEmpty = hideEmpty; save(); }
        DrawDockSettings(HudEdges, panel.Docked, x => panel.Docked = x, panel.Edge, x => panel.Edge = x,
            panel.AutoHide, x => panel.AutoHide = x, panel.Position, x => panel.Position = x);
        PercentSlider("Width", panel.Width, 60, 160, 100, x => panel.Width = x);
        PercentSlider("Opacity", panel.Opacity, 25, 100, 100, x => panel.Opacity = x);
        PercentSlider("Background opacity", panel.BackgroundOpacity, 0, 100, 100, x => panel.BackgroundOpacity = x);
        ImGui.PopID();
    }

    private void DrawAboutSettings()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted("Amor RP " + PluginVersion);
        ImGui.TextUnformatted("Server version (last connected): " + (connectedServerVersion ?? "Not connected"));
        if (connectedServerVersion != null && session != null)
            ImGui.TextDisabled("Seasons");
        ImGui.Spacing();
        Wrapped("Settings: /amorrp settings · Activity Log: /amorrp log");
        Wrapped("Windows: /amorrp season (Home), /amorrp inventory, /amorrp crafting, /amorrp actions, /amorrp writing, /amorrp groups, /amorrp manage and /amorrp trade");
        ImGui.Spacing();
        if (ImGui.Button("Copy version info"))
            ImGui.SetClipboardText($"Amor RP plugin: {PluginVersion}\nServer (last connected): {connectedServerVersion ?? "Not connected"}");
        if (CheckForPluginUpdates is { } checkForUpdates)
        {
            ImGui.SameLine();
            if (ImGui.Button("Check for updates")) checkForUpdates();
        }
    }
}
