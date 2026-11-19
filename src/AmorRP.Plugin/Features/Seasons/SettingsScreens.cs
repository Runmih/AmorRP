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

    private void DrawInterfaceSettings()
    {
        ImGui.Spacing();
        var show = configuration.ShowLauncher;
        if (ImGui.Checkbox("Show quick launcher", ref show))
        { configuration.ShowLauncher = show; save(); SetLauncherVisible?.Invoke(show); }
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
