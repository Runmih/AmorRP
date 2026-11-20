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
    public bool QuickBarDocked { get; set; } = true;
    public float QuickBarFloatX { get; set; } = 10;
    public float QuickBarFloatY { get; set; } = 30;
    public Features.Shell.LauncherEdge FavoriteEventsEdge { get; set; } = Features.Shell.LauncherEdge.Right;
    public bool FavoriteEventsDocked { get; set; }
    public bool FavoriteEventsAutoHide { get; set; } = true;
    public float FavoriteEventsPosition { get; set; } = 50;
    public float FavoriteEventsFloatX { get; set; } = 5;
    public float FavoriteEventsFloatY { get; set; } = 30;
    public float FavoriteEventsSize { get; set; } = 100;
    public float FavoriteEventsOpacity { get; set; } = 100;
    public float FavoriteEventsBackgroundOpacity { get; set; } = 100;
    public bool ShowAttachedInterfaces { get; set; } = true;
    public Dictionary<string, Guid[]> PinnedHoldings { get; set; } = [];
    public List<Services.Identity.SavedSession> SavedSessions { get; set; } = [];
    public List<Services.Identity.PendingCommand> PendingCommands { get; set; } = [];
}
