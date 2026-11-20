using Dalamud.Configuration;

namespace AmorRP.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public Dictionary<string, string> ProtectedMemberNotes { get; set; } = [];
    public bool ShowFavoriteEvents { get; set; } = true;
    public bool ShowLauncher { get; set; } = true;
    public Features.Shell.LauncherEdge QuickBarEdge { get; set; } = Features.Shell.LauncherEdge.Left;
    public bool QuickBarAutoHide { get; set; } = true;
    public float QuickBarSize { get; set; } = 100;
    public float QuickBarOpacity { get; set; } = 100;
    public float QuickBarPosition { get; set; } = 50;
    public bool ShowAttachedInterfaces { get; set; } = true;
    public Dictionary<string, Guid[]> PinnedHoldings { get; set; } = [];
    public List<Services.Identity.SavedSession> SavedSessions { get; set; } = [];
    public List<Services.Identity.PendingCommand> PendingCommands { get; set; } = [];
}
