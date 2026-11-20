using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/amorrp";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IPlayerState playerState;
    private readonly List<TitleBarButton> logButtons = [];
    private readonly ActivityLogWindow activityLogWindow;
    private readonly WindowSystem windows = new("AmorRP");
    private readonly WindowSystem updateWindows = new("AmorRPUpdate");
    private readonly PluginUpdateCheck updates = new(typeof(Plugin).Assembly.GetName().Version!);
    private readonly PluginUpdateWindow updateWindow;
    private PluginUpdateStatus? announcedUpdateStatus;
    private bool GameplayAvailable => updates.Status == PluginUpdateStatus.Current;
    private readonly Configuration configuration;
    private readonly LauncherWindow launcherWindow;
    private readonly HubWindow hub;
    private readonly Dictionary<WorkspaceKind, WorkspaceWindow> workspaceWindows = [];
    private readonly FavoriteEventsWindow favoriteEventsWindow;
    private readonly MainWindow mainWindow;
    private readonly AmorRP.Plugin.Features.Inventory.CompactWindow compactWindow;
    private readonly AmorRP.Plugin.Features.Trading.TradeWindow tradeWindow;
    private readonly AmorRP.Plugin.Features.Seasons.SeasonFormWindow createCommunityWindow;
    private readonly CharacterContext context;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPlayerState playerState,
        IContextMenu contextMenu, IPartyList party, ITextureProvider textures)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.playerState = playerState;
        updateWindow = new(updates, () => pluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.UpdateablePlugins, "Amor RP"));
        updateWindows.AddWindow(updateWindow);
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (configuration.Version < 4) { configuration.Version = 4; pluginInterface.SavePluginConfig(configuration); }
        mainWindow = new MainWindow(playerState, party, configuration, () => pluginInterface.SavePluginConfig(configuration), textures);
        hub = new(configuration, mainWindow.DrawWorkspace, () => playerState.IsLoaded);
        foreach (var kind in Enum.GetValues<WorkspaceKind>().Where(kind => !HubWindow.Contains(kind)))
            workspaceWindows[kind] = new(kind, mainWindow.DrawWorkspace, () => playerState.IsLoaded);
        mainWindow.OpenWorkspace = kind =>
        {
            if (HubWindow.Contains(kind)) hub.Show(kind);
            else workspaceWindows[kind].IsOpen = true;
        };
        compactWindow = new(mainWindow.DrawCompact);
        tradeWindow = new(mainWindow.DrawTrade, mainWindow.TradeOpened, mainWindow.TradeClosed);
        mainWindow.OpenTrade = () => tradeWindow.IsOpen = true;
        createCommunityWindow = new("Amor RP — Prepare season###AmorRPCreateCommunity", mainWindow.DrawCreateCommunity, () => mainWindow.CanDrawCommunityForms, new(560, 660), mainWindow.ClearFeedback);
        mainWindow.OpenCreateCommunity = () => createCommunityWindow.IsOpen = true;
        mainWindow.CloseCreateCommunity = () => createCommunityWindow.IsOpen = false;
        activityLogWindow = new(mainWindow.DrawActivityLog);
        launcherWindow = new(configuration, () => pluginInterface.SavePluginConfig(configuration), Open, OpenLog,
            () => hub.TogglePage(WorkspaceKind.Writing),
            () => ToggleWindow(tradeWindow, mainWindow.OpenTradeFromLauncher),
            ToggleWorkspace,
            () => IsWorkspaceOpen(WorkspaceKind.Writing), () => tradeWindow.IsOpen, IsWorkspaceOpen,
            () => mainWindow.PendingTradeRequests, () => mainWindow.UnreadAnnouncements, () => playerState.IsLoaded,
            () => configuration.ShowAttachedInterfaces, ToggleAttachedInterfaces,
            () => mainWindow.CanOpenGroupManagement, () => mainWindow.CanOpenCommunityManagement)
            { IsOpen = configuration.ShowLauncher };
        mainWindow.SetLauncherVisible = visible => launcherWindow.IsOpen = visible;
        context = new(contextMenu, mainWindow.SelectTarget, () => GameplayAvailable);
        favoriteEventsWindow = new(configuration, () => pluginInterface.SavePluginConfig(configuration), mainWindow.UpcomingFavoriteEvents, mainWindow.DrawFavoriteEvents,
            () => playerState.IsLoaded && configuration.ShowAttachedInterfaces) { IsOpen = configuration.ShowFavoriteEvents };
        mainWindow.SetFavoriteEventsVisible = visible => favoriteEventsWindow.IsOpen = visible;
        windows.AddWindow(favoriteEventsWindow);
        foreach (var menu in (Window[])[mainWindow, hub, compactWindow, tradeWindow, createCommunityWindow, .. workspaceWindows.Values])
        {
            if (MenuWindowButtons.Add(menu, Open, OpenLog) is { } logButton) logButtons.Add(logButton);
            windows.AddWindow(menu);
        }
        MenuWindowButtons.Add(activityLogWindow, Open, OpenLog, isLog: true);
        windows.AddWindow(activityLogWindow);
        windows.AddWindow(launcherWindow);
        commands.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "/amorrp → Settings" });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        pluginInterface.UiBuilder.OpenConfigUi += Open;
    }

    private void Draw()
    {
        updates.Tick(DateTimeOffset.UtcNow);
        using var theme = PluginTheme.Apply();
        if (!GameplayAvailable)
        {
            var status = updates.Status;
            if (playerState.IsLoaded && (status is PluginUpdateStatus.Required or PluginUpdateStatus.Unavailable)
                && announcedUpdateStatus != status)
            {
                announcedUpdateStatus = status;
                ShowUpdateWindow();
            }
            updateWindows.Draw();
            return;
        }
        announcedUpdateStatus = null;
        updateWindow.IsOpen = false;
        mainWindow.SetInventoryVisible(IsWorkspaceOpen(WorkspaceKind.Inventory) && playerState.IsLoaded);
        mainWindow.SetWritingLogVisible(IsWorkspaceOpen(WorkspaceKind.Writing) && playerState.IsLoaded);
        mainWindow.Tick();
        foreach (var button in logButtons)
            button.IconColor = mainWindow.HasPendingAction ? PluginTheme.Accent : null;
        windows.Draw();
    }

    private void OnCommand(string command, string arguments)
    {
        if (!GameplayAvailable) { ShowUpdateWindow(); return; }
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "log": OpenLog(); break;
            case "settings": Open(); break;
            case "compact": compactWindow.Toggle(); break;
            case "trade": mainWindow.OpenTradeFromLauncher(); break;
            case "writing": mainWindow.ShowWorkspace(WorkspaceKind.Writing); break;
            case "inventory": mainWindow.ShowWorkspace(WorkspaceKind.Inventory); break;
            case "crafting": mainWindow.ShowWorkspace(WorkspaceKind.Crafting); break;
            case "groups": mainWindow.ShowWorkspace(WorkspaceKind.GroupManagement); break;
            case "manage": mainWindow.ShowWorkspace(WorkspaceKind.CommunityManagement); break;
            case "season": mainWindow.ShowWorkspace(WorkspaceKind.Season); break;
            case "launcher":
                configuration.ShowLauncher = !configuration.ShowLauncher; launcherWindow.IsOpen = configuration.ShowLauncher;
                pluginInterface.SavePluginConfig(configuration); break;
            case "panels": ToggleAttachedInterfaces(); break;
            default: mainWindow.Toggle(); break;
        }
    }
    private void Open()
    {
        if (!GameplayAvailable) { ShowUpdateWindow(); return; }
        mainWindow.IsOpen = true;
        mainWindow.BringToFront();
    }

    private void ShowUpdateWindow()
    {
        updateWindow.IsOpen = true;
        updateWindow.BringToFront();
    }

    private void OpenLog()
    {
        activityLogWindow.IsOpen = true;
        activityLogWindow.BringToFront();
    }

    private bool IsWorkspaceOpen(WorkspaceKind kind) =>
        HubWindow.Contains(kind) ? hub.IsOpen && hub.Page == kind : workspaceWindows[kind].IsOpen;

    private void ToggleWorkspace(WorkspaceKind kind)
    {
        if (HubWindow.Contains(kind)) hub.TogglePage(kind);
        else ToggleWindow(workspaceWindows[kind], () => mainWindow.ShowWorkspace(kind));
    }

    private static void ToggleWindow(Window window, Action open)
    {
        if (window.IsOpen) window.IsOpen = false;
        else open();
    }

    private void ToggleAttachedInterfaces()
    {
        configuration.ShowAttachedInterfaces = !configuration.ShowAttachedInterfaces;
        pluginInterface.SavePluginConfig(configuration);
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenMainUi -= Open;
        pluginInterface.UiBuilder.OpenConfigUi -= Open;
        commands.RemoveHandler(Command);
        context.Dispose();
        updateWindows.RemoveAllWindows();
        windows.RemoveAllWindows();
        updates.Dispose();
        mainWindow.Dispose();
    }
}
