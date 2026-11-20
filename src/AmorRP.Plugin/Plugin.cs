using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Game;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Game.Command;
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
    private readonly Configuration configuration;
    private readonly LauncherWindow launcherWindow;
    private readonly AmorRP.Plugin.Features.Writing.WritingLogWindow writingLogWindow;
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
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (configuration.Version < 4) { configuration.Version = 4; pluginInterface.SavePluginConfig(configuration); }
        mainWindow = new MainWindow(playerState, party, configuration, () => pluginInterface.SavePluginConfig(configuration), textures);
        foreach (var kind in Enum.GetValues<WorkspaceKind>())
            workspaceWindows[kind] = new(kind, mainWindow.DrawWorkspace, () => playerState.IsLoaded);
        mainWindow.OpenWorkspace = kind => workspaceWindows[kind].IsOpen = true;
        writingLogWindow = new(mainWindow.DrawWritingLog, () => playerState.IsLoaded);
        mainWindow.OpenWritingLog = () => writingLogWindow.IsOpen = true;
        compactWindow = new(mainWindow.DrawCompact);
        tradeWindow = new(mainWindow.DrawTrade, mainWindow.TradeOpened, mainWindow.TradeClosed);
        mainWindow.OpenTrade = () => tradeWindow.IsOpen = true;
        createCommunityWindow = new("Amor RP — Prepare season###AmorRPCreateCommunity", mainWindow.DrawCreateCommunity, () => mainWindow.CanDrawCommunityForms, new(560, 660), mainWindow.ClearFeedback);
        mainWindow.OpenCreateCommunity = () => createCommunityWindow.IsOpen = true;
        mainWindow.CloseCreateCommunity = () => createCommunityWindow.IsOpen = false;
        activityLogWindow = new(mainWindow.DrawActivityLog);
        launcherWindow = new(configuration, () => pluginInterface.SavePluginConfig(configuration), Open, OpenLog,
            () => ToggleWindow(writingLogWindow, mainWindow.OpenWritingLogFromLauncher),
            () => ToggleWindow(tradeWindow, mainWindow.OpenTradeFromLauncher),
            kind => ToggleWindow(workspaceWindows[kind], () => mainWindow.ShowWorkspace(kind)),
            () => writingLogWindow.IsOpen, () => tradeWindow.IsOpen, kind => workspaceWindows[kind].IsOpen,
            () => mainWindow.PendingTradeRequests, () => mainWindow.UnreadAnnouncements, () => playerState.IsLoaded,
            () => configuration.ShowAttachedInterfaces, ToggleAttachedInterfaces,
            () => mainWindow.CanOpenGroupManagement, () => mainWindow.CanOpenCommunityManagement)
            { IsOpen = configuration.ShowLauncher };
        mainWindow.SetLauncherVisible = visible => launcherWindow.IsOpen = visible;
        context = new(contextMenu, mainWindow.SelectTarget);
        favoriteEventsWindow = new(configuration, () => pluginInterface.SavePluginConfig(configuration), mainWindow.DrawFavoriteEvents,
            () => playerState.IsLoaded && configuration.ShowAttachedInterfaces, Open, OpenLog) { IsOpen = configuration.ShowFavoriteEvents };
        mainWindow.SetFavoriteEventsVisible = visible => favoriteEventsWindow.IsOpen = visible;
        windows.AddWindow(favoriteEventsWindow);
        foreach (var menu in (Window[])[mainWindow, writingLogWindow, compactWindow, tradeWindow, createCommunityWindow, .. workspaceWindows.Values])
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
        mainWindow.SetInventoryVisible(workspaceWindows[WorkspaceKind.Inventory].IsOpen && playerState.IsLoaded);
        mainWindow.SetWritingLogVisible(writingLogWindow.IsOpen && playerState.IsLoaded);
        mainWindow.Tick();
        foreach (var button in logButtons)
            button.IconColor = mainWindow.HasPendingAction ? PluginTheme.Accent : null;
        using var theme = PluginTheme.Apply();
        windows.Draw();
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "log": OpenLog(); break;
            case "settings": Open(); break;
            case "compact": compactWindow.Toggle(); break;
            case "trade": mainWindow.OpenTradeFromLauncher(); break;
            case "writing": mainWindow.OpenWritingLogFromLauncher(); break;
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
        mainWindow.IsOpen = true;
        mainWindow.BringToFront();
    }

    private void OpenLog()
    {
        activityLogWindow.IsOpen = true;
        activityLogWindow.BringToFront();
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
        windows.RemoveAllWindows();
        mainWindow.Dispose();
    }
}
