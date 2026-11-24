using AmorRP.Plugin.Features.Shell;

using Dalamud.Configuration;

namespace AmorRP.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public Dictionary<string, string> ProtectedMemberNotes { get; set; } = [];
    public CoinHudSettings CoinHud { get; set; } = new();
    public CoinHudSettings HealthHud { get; set; } = new() { FloatX = 85, FloatY = 70 };
    public bool ShowFavoriteEvents { get; set; } = true;
    public bool ShowLauncher { get; set; } = true;
    public Features.Shell.LauncherEdge QuickBarEdge { get; set; } = Features.Shell.LauncherEdge.Left;
    public bool QuickBarAutoHide { get; set; } = true;
    public float QuickBarSize { get; set; } = 100;
    public float QuickBarOpacity { get; set; } = 100;
    public float QuickBarPosition { get; set; } = 50;
    public Features.Shell.LauncherEdge HubNavigationEdge { get; set; } = Features.Shell.LauncherEdge.Left;
    public bool FavoriteEventsShowHeader { get; set; } = true;
    public bool FavoriteEventsHideWhenEmpty { get; set; }
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

public sealed class CoinHudSettings
{
    public bool Visible { get; set; } = true;
    public bool ShowHeader { get; set; } = true;
    public bool HideWhenEmpty { get; set; } = true;
    public LauncherEdge Edge { get; set; } = LauncherEdge.Right;
    public bool Docked { get; set; }
    public bool AutoHide { get; set; } = true;
    public float Position { get; set; } = 50;
    public float FloatX { get; set; } = 85;
    public float FloatY { get; set; } = 80;
    public float Width { get; set; } = 100;
    public float Opacity { get; set; } = 100;
    public float BackgroundOpacity { get; set; } = 100;

    internal HudLayout Layout() => new(Docked, Edge, AutoHide, Position, new(FloatX, FloatY),
        Width, Opacity, BackgroundOpacity, ShowHeader, HideWhenEmpty);

    internal void Move(System.Numerics.Vector2 next)
    {
        if (Docked) Position = next.X;
        else { FloatX = next.X; FloatY = next.Y; }
    }
}
