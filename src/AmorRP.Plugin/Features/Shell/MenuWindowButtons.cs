using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

internal static class MenuWindowButtons
{
    public static TitleBarButton? Add(Window window, Action openSettings, Action openLog, Action refresh, bool isLog = false)
    {
        window.TitleBarButtons.Add(Button(FontAwesomeIcon.Cog, "Settings", 0, openSettings));
        window.TitleBarButtons.Add(Button(FontAwesomeIcon.SyncAlt, "Refresh", 1, refresh));
        if (isLog) return null;
        var log = Button(FontAwesomeIcon.History, "Activity Log", 2, openLog);
        window.TitleBarButtons.Add(log);
        return log;
    }

    private static TitleBarButton Button(FontAwesomeIcon icon, string label, int priority, Action open) => new()
    {
        Icon = icon,
        Priority = priority,
        Click = button => { if (button == ImGuiMouseButton.Left) open(); },
        ShowTooltip = () => ImGui.SetTooltip(label)
    };
}
