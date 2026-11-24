using AmorRP.Plugin.Features.Seasons;
using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Features.Trading;
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
    private const string WindowNamespace = "AmorRP";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IPlayerState playerState;
    private readonly List<TitleBarButton> logButtons = [];
    private readonly ActivityLogWindow activityLogWindow;
    private readonly WindowSystem windows = new(WindowNamespace);
    private readonly WindowSystem launcherWindows = new(WindowNamespace);
    private readonly WindowSystem updateWindows = new("AmorRPUpdate");
    private readonly PluginUpdateCheck updates;
    private readonly PluginUpdateWindow updateWindow;
    private bool showVersionStatus;
    private bool GameplayAvailable => updates.Status == PluginUpdateStatus.Current;
    private readonly Configuration configuration;
    private readonly LauncherWindow launcherWindow;
    private readonly HubWindow hub;
    private readonly Dictionary<WorkspaceKind, WorkspaceWindow> workspaceWindows = [];
    private readonly MainWindow mainWindow;
    private readonly AmorRP.Plugin.Features.Inventory.CompactWindow compactWindow;
    private readonly DockedHudWindow<AmorRP.Contracts.Combat.CombatSheet> actionsWindow;
    private readonly TradeWindow tradeWindow;
    private readonly TradeRequestWindow tradeRequestWindow;
    private readonly SeasonFormWindow createCommunityWindow;
    private readonly CharacterContext context;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPlayerState playerState,
        IContextMenu contextMenu, IPartyList party, ITextureProvider textures, IObjectTable objects, ITargetManager targets)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.playerState = playerState;
        updates = new(typeof(Plugin).Assembly.GetName().Version!, readInstallerVersion: ReadDalamudUpdateVersion);
        updateWindow = new(updates, () => pluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.UpdateablePlugins, "Amor RP"));
        updateWindows.AddWindow(updateWindow);
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (configuration.Version < 4) { configuration.Version = 4; pluginInterface.SavePluginConfig(configuration); }
        mainWindow = new MainWindow(playerState, party, configuration, () => pluginInterface.SavePluginConfig(configuration), textures, pluginInterface.UiBuilder.FontAtlas, targets);
        mainWindow.Moodles = new MoodleAdapter(pluginInterface, objects);
        mainWindow.CheckForPluginUpdates = CheckForUpdates;
        hub = new(configuration, mainWindow.DrawWorkspace, () => playerState.IsLoaded, () => mainWindow.CanOpenRecipeCreation);
        foreach (var kind in Enum.GetValues<WorkspaceKind>().Where(kind => kind != WorkspaceKind.Groups && !HubWindow.Contains(kind)))
            workspaceWindows[kind] = new(kind, mainWindow.DrawWorkspace, () => playerState.IsLoaded);
        mainWindow.OpenWorkspace = kind =>
        {
            if (kind == WorkspaceKind.Groups)
            {
                mainWindow.SelectGroupsPage();
                hub.Show(WorkspaceKind.Season);
            }
            else if (HubWindow.Contains(kind)) hub.Show(kind);
            else
            {
                var window = workspaceWindows[kind];
                window.IsOpen = true;
                window.BringToFront();
            }
        };
        compactWindow = new(mainWindow.DrawCompact);
        tradeWindow = new(mainWindow.DrawTrade, mainWindow.TradeOpened, mainWindow.TradeClosed);
        tradeRequestWindow = new(mainWindow.DrawTradeRequest);
        mainWindow.OpenTrade = () =>
        {
            tradeWindow.IsOpen = true;
            tradeWindow.BringToFront();
        };
        mainWindow.SetTradeRequest = visible =>
        {
            tradeRequestWindow.IsOpen = visible;
            if (visible) tradeRequestWindow.BringToFront();
        };
        createCommunityWindow = new("Amor RP — Prepare season###AmorRPCreateCommunity", mainWindow.DrawCreateCommunity, () => mainWindow.CanDrawCommunityForms, new(560, 660), mainWindow.ClearFeedback);
        mainWindow.OpenCreateCommunity = () => createCommunityWindow.IsOpen = true;
        mainWindow.CloseCreateCommunity = () => createCommunityWindow.IsOpen = false;
        activityLogWindow = new(mainWindow.DrawActivityLog);
        launcherWindow = new(configuration, () => pluginInterface.SavePluginConfig(configuration), Open, OpenLog,
            () => ToggleWorkspace(WorkspaceKind.Writing),
            ToggleTrade,
            ToggleWorkspace,
            () => IsWorkspaceOpen(WorkspaceKind.Writing), () => GameplayAvailable && (tradeWindow.IsOpen || tradeRequestWindow.IsOpen), IsWorkspaceOpen,
            () => GameplayAvailable ? mainWindow.PendingTradeRequests : 0,
            () => GameplayAvailable ? mainWindow.UnreadAnnouncements : 0, () => playerState.IsLoaded,
            () => configuration.ShowAttachedInterfaces, ToggleAttachedInterfaces,
            () => mainWindow.CanOpenCommunityManagement)
            { IsOpen = configuration.ShowLauncher };
        mainWindow.SetLauncherVisible = visible => launcherWindow.IsOpen = visible;
        var billingForm = new SeasonFormWindow("Amor RP — Billing###AmorRPContextBilling", mainWindow.DrawContextBilling,
            () => playerState.IsLoaded, new(420, 275), mainWindow.ClearFeedback, mainWindow.ClearContextBilling);
        context = new(contextMenu, mainWindow.SelectTarget, target =>
        {
            if (!mainWindow.OpenContextBilling(target)) return;
            billingForm.IsOpen = true;
            billingForm.BringToFront();
        }, () => GameplayAvailable, () => mainWindow.CanOpenContextBilling, mainWindow.OpenPrivatePermit, () => mainWindow.CanOfferPrivatePermit);
        void SaveHud() => pluginInterface.SavePluginConfig(configuration);
        bool HudAvailable() => playerState.IsLoaded && configuration.ShowAttachedInterfaces;
        var favoriteEventsWindow = new DockedHudWindow<AmorRP.Contracts.Groups.GroupEvent>(
            "Amor RP — Favorite events###AmorRPFavoriteEvents", "##EventsContent",
            () => new(configuration.FavoriteEventsDocked, configuration.FavoriteEventsEdge, configuration.FavoriteEventsAutoHide,
                configuration.FavoriteEventsPosition, new(configuration.FavoriteEventsFloatX, configuration.FavoriteEventsFloatY),
                configuration.FavoriteEventsSize, configuration.FavoriteEventsOpacity, configuration.FavoriteEventsBackgroundOpacity,
                configuration.FavoriteEventsShowHeader, configuration.FavoriteEventsHideWhenEmpty),
            next =>
            {
                if (configuration.FavoriteEventsDocked) configuration.FavoriteEventsPosition = next.X;
                else { configuration.FavoriteEventsFloatX = next.X; configuration.FavoriteEventsFloatY = next.Y; }
            },
            () => configuration.FavoriteEventsDocked = !configuration.FavoriteEventsDocked,
            SaveHud, mainWindow.UpcomingFavoriteEvents, mainWindow.DrawFavoriteEvents, HudAvailable)
            { IsOpen = configuration.ShowFavoriteEvents };
        mainWindow.SetFavoriteEventsVisible = visible => favoriteEventsWindow.IsOpen = visible;
        windows.AddWindow(favoriteEventsWindow);
        var coin = configuration.CoinHud;
        var coinWindow = new DockedHudWindow<string>(
            "Amor RP — Coins###AmorRPCoins", "##CoinContent", coin.Layout, coin.Move, () => coin.Docked = !coin.Docked,
            SaveHud, mainWindow.CoinBalance, mainWindow.DrawCoinHud, HudAvailable) { IsOpen = coin.Visible };
        mainWindow.SetCoinHudVisible = visible => coinWindow.IsOpen = visible;
        windows.AddWindow(coinWindow);
        var health = configuration.HealthHud;
        var healthWindow = new DockedHudWindow<AmorRP.Contracts.Combat.CombatSheet>(
            "Amor RP — Health###AmorRPHealth", "##HealthContent", health.Layout, health.Move, () => health.Docked = !health.Docked,
            SaveHud, mainWindow.HealthHudRows, mainWindow.DrawHealthHud, HudAvailable) { IsOpen = health.Visible };
        mainWindow.SetHealthHudVisible = visible => healthWindow.IsOpen = visible;
        windows.AddWindow(healthWindow);
        var actions = configuration.ActionsHud;
        actionsWindow = new("Amor RP — Actions###AmorRPCombatActionsWorkspace", "##ActionsContent",
            actions.Layout, actions.Move, () => actions.Docked = !actions.Docked, SaveHud,
            mainWindow.ActionsHudRows, mainWindow.DrawActionsHud, () => playerState.IsLoaded, baseWidth: 280, frameRows: mainWindow.ActionsHudFrameRows)
            { IsOpen = actions.Visible };
        mainWindow.SetActionsHudVisible = visible =>
        {
            actionsWindow.IsOpen = visible;
            if (visible) actionsWindow.Reveal();
        };
        windows.AddWindow(actionsWindow);
        foreach (var menu in (Window[])[mainWindow, hub, compactWindow, tradeWindow, tradeRequestWindow, createCommunityWindow, billingForm, .. workspaceWindows.Values])
        {
            if (MenuWindowButtons.Add(menu, Open, OpenLog, mainWindow.RefreshData) is { } logButton) logButtons.Add(logButton);
            windows.AddWindow(menu);
        }
        MenuWindowButtons.Add(activityLogWindow, Open, OpenLog, mainWindow.RefreshData, isLog: true);
        windows.AddWindow(activityLogWindow);
        launcherWindows.AddWindow(launcherWindow);
        commands.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "/amorrp → Settings" });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        pluginInterface.UiBuilder.OpenConfigUi += Open;
    }

    private void Draw()
    {
        updates.Tick(DateTimeOffset.UtcNow);
        using var theme = PluginTheme.Apply();
        if (GameplayAvailable)
        {
            if (!showVersionStatus) updateWindow.IsOpen = false;
            mainWindow.SetInventoryVisible(IsWorkspaceOpen(WorkspaceKind.Inventory) && playerState.IsLoaded);
            mainWindow.SetWritingLogVisible(IsWorkspaceOpen(WorkspaceKind.Writing) && playerState.IsLoaded);
            mainWindow.Tick();
            foreach (var button in logButtons)
                button.IconColor = mainWindow.HasPendingAction ? PluginTheme.Accent : null;
            windows.Draw();
        }
        launcherWindows.Draw();
        updateWindows.Draw();
        if (!updateWindow.IsOpen) showVersionStatus = false;
    }

    private void OnCommand(string command, string arguments)
    {
        if (!TryUseGameplay()) return;
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "log": OpenLog(); break;
            case "settings": Open(); break;
            case "compact": compactWindow.Toggle(); break;
            case "trade": mainWindow.OpenTradeFromLauncher(); break;
            case "writing": mainWindow.ShowWorkspace(WorkspaceKind.Writing); break;
            case "inventory": mainWindow.ShowWorkspace(WorkspaceKind.Inventory); break;
            case "crafting": mainWindow.ShowWorkspace(WorkspaceKind.Crafting); break;
            case "actions": ShowCombatActions(); break;
            case "groups": mainWindow.ShowWorkspace(WorkspaceKind.Groups); break;
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
        if (!TryUseGameplay()) return;
        mainWindow.IsOpen = true;
        mainWindow.BringToFront();
    }

    private bool TryUseGameplay()
    {
        if (GameplayAvailable) return true;
        ShowUpdateWindow();
        return false;
    }

    private void ShowUpdateWindow()
    {
        updateWindow.IsOpen = true;
        updateWindow.BringToFront();
    }

    private void CheckForUpdates()
    {
        updates.Retry();
        showVersionStatus = true;
        ShowUpdateWindow();
    }

    private async Task<Version?> ReadDalamudUpdateVersion(CancellationToken cancellationToken)
    {
        var update = await pluginInterface.CheckForUpdateAsync()
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        return update?.Version;
    }

    private void OpenLog()
    {
        if (!TryUseGameplay()) return;
        activityLogWindow.IsOpen = true;
        activityLogWindow.BringToFront();
    }

    private bool IsWorkspaceOpen(WorkspaceKind kind) => GameplayAvailable
        && (kind == WorkspaceKind.Groups ? hub.IsOpen && hub.Page == WorkspaceKind.Season && mainWindow.GroupsSelected
            : HubWindow.Contains(kind) ? hub.IsOpen && hub.Page == kind : workspaceWindows[kind].IsOpen);

    private void ToggleWorkspace(WorkspaceKind kind)
    {
        if (!TryUseGameplay()) return;
        if (kind == WorkspaceKind.Groups)
        {
            if (IsWorkspaceOpen(kind)) hub.IsOpen = false;
            else mainWindow.ShowWorkspace(kind);
        }
        else if (HubWindow.Contains(kind)) hub.TogglePage(kind);
        else ToggleWindow(workspaceWindows[kind], () => mainWindow.ShowWorkspace(kind));
    }

    private void ToggleTrade()
    {
        if (!TryUseGameplay()) return;
        if (tradeWindow.IsOpen) tradeWindow.IsOpen = false;
        else ToggleWindow(tradeRequestWindow, mainWindow.OpenTradeFromLauncher);
    }

    private static void ToggleWindow(Window window, Action open)
    {
        if (window.IsOpen) window.IsOpen = false;
        else open();
    }

    private void ShowCombatActions()
    {
        if (!configuration.ActionsHud.Visible)
        {
            configuration.ActionsHud.Visible = true;
            pluginInterface.SavePluginConfig(configuration);
        }
        actionsWindow.IsOpen = true;
        actionsWindow.Reveal();
        actionsWindow.BringToFront();
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
        launcherWindows.RemoveAllWindows();
        windows.RemoveAllWindows();
        updates.Dispose();
        mainWindow.Dispose();
    }
}
