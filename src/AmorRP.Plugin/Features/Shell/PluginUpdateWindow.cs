using System.Numerics;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

internal sealed class PluginUpdateWindow : Window
{
    private readonly PluginUpdateCheck updates;
    private readonly Action openInstaller;

    public PluginUpdateWindow(PluginUpdateCheck updates, Action openInstaller)
        : base("Amor RP — Plugin update###AmorRPPluginUpdate", ImGuiWindowFlags.NoCollapse)
    {
        this.updates = updates;
        this.openInstaller = openInstaller;
        Size = new Vector2(470, 210);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (updates.IsChecking && updates.Status != PluginUpdateStatus.Required)
        {
            ImGui.TextUnformatted("Checking the latest Amor RP version…");
            return;
        }

        switch (updates.Status)
        {
            case PluginUpdateStatus.Required:
                ImGui.TextColored(PluginTheme.Accent, "Update required");
                ImGui.TextUnformatted($"Installed {updates.InstalledVersion}   ·   Latest {updates.PublishedVersion}");
                ImGui.Spacing();
                ImGui.TextWrapped("Update Amor RP in the Dalamud Plugin Installer to continue.");
                ImGui.Spacing();
                if (ImGui.Button("Open plugin updates")) openInstaller();
                break;
            case PluginUpdateStatus.Unavailable:
                ImGui.TextColored(PluginTheme.Accent, "Could not verify the latest version");
                ImGui.TextWrapped("Amor RP is paused until it can reach the installer feed.");
                ImGui.Spacing();
                if (ImGui.Button("Retry")) updates.Retry();
                break;
            case PluginUpdateStatus.Current:
                ImGui.TextColored(PluginTheme.Accent, "Amor RP is up to date");
                ImGui.TextUnformatted($"Installed {updates.InstalledVersion}   ·   Latest {updates.PublishedVersion}");
                break;
            default:
                ImGui.TextUnformatted("Checking the latest Amor RP version…");
                break;
        }
    }
}
