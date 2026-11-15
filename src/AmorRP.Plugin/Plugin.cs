using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Game;
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
    private readonly WindowSystem windows = new("AmorRP");
    private readonly Configuration configuration;
    private readonly LauncherWindow launcherWindow;
    private readonly AmorRP.Plugin.Features.Writing.WritingLogWindow writingLogWindow;
    private readonly FavoriteEventsWindow favoriteEventsWindow;
    private readonly MainWindow mainWindow;
    private readonly AmorRP.Plugin.Features.Inventory.CompactWindow compactWindow;
    private readonly AmorRP.Plugin.Features.Inventory.InventoryCompanionWindow inventoryCompanionWindow;
    private readonly AmorRP.Plugin.Features.Trading.TradeWindow tradeWindow;
    private readonly AmorRP.Plugin.Features.Communities.CommunityFormWindow joinCommunityWindow, createCommunityWindow;
    private readonly CharacterContext context;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPlayerState playerState,
        IContextMenu contextMenu, IPartyList party, ITextureProvider textures, IGameGui gameGui)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.playerState = playerState;
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (configuration.Version < 4) { configuration.Version = 4; pluginInterface.SavePluginConfig(configuration); }
        mainWindow = new MainWindow(playerState, party, configuration, () => pluginInterface.SavePluginConfig(configuration), textures);
        writingLogWindow = new(mainWindow.DrawWritingLog, () => playerState.IsLoaded);
        mainWindow.OpenWritingLog = () => writingLogWindow.IsOpen = true;
        compactWindow = new(mainWindow.DrawCompact);
        inventoryCompanionWindow = new(gameGui, () => configuration.ShowAttachedInterfaces && configuration.ShowInventoryCompanion && playerState.IsLoaded, mainWindow.DrawInventoryCompanion);
        tradeWindow = new(mainWindow.DrawTrade, mainWindow.TradeOpened, mainWindow.TradeClosed);
        mainWindow.OpenTrade = () => tradeWindow.IsOpen = true;
        joinCommunityWindow = new("Amor RP — Join community###AmorRPJoinCommunity", mainWindow.DrawJoinCommunity, () => mainWindow.CanDrawCommunityForms, new(520, 360), mainWindow.ClearFeedback);
        createCommunityWindow = new("Amor RP — Create community###AmorRPCreateCommunity", mainWindow.DrawCreateCommunity, () => mainWindow.CanDrawCommunityForms, new(560, 660), mainWindow.ClearFeedback);
        mainWindow.OpenJoinCommunity = () => joinCommunityWindow.IsOpen = true;
        mainWindow.OpenCreateCommunity = () => createCommunityWindow.IsOpen = true;
        mainWindow.CloseJoinCommunity = () => joinCommunityWindow.IsOpen = false;
        mainWindow.CloseCreateCommunity = () => createCommunityWindow.IsOpen = false;
        launcherWindow = new(mainWindow.OpenWritingLogFromLauncher, mainWindow.OpenTradeFromLauncher,
            () => mainWindow.PendingTradeRequests, () => playerState.IsLoaded,
            () => configuration.ShowAttachedInterfaces, ToggleAttachedInterfaces)
            { IsOpen = configuration.ShowLauncher };
        mainWindow.SetLauncherVisible = visible => launcherWindow.IsOpen = visible;
        context = new(contextMenu, mainWindow.SelectTarget);
        favoriteEventsWindow = new(mainWindow.DrawFavoriteEvents, () => playerState.IsLoaded && configuration.ShowAttachedInterfaces) { IsOpen = configuration.ShowFavoriteEvents };
        mainWindow.SetFavoriteEventsVisible = visible => favoriteEventsWindow.IsOpen = visible;
        windows.AddWindow(favoriteEventsWindow);
        windows.AddWindow(mainWindow);
        windows.AddWindow(writingLogWindow);
        windows.AddWindow(compactWindow);
        windows.AddWindow(inventoryCompanionWindow);
        windows.AddWindow(tradeWindow);
        windows.AddWindow(joinCommunityWindow);
        windows.AddWindow(createCommunityWindow);
        windows.AddWindow(launcherWindow);
        commands.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "/amorrp → Main Menu" });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        pluginInterface.UiBuilder.OpenConfigUi += Open;
    }

    private void Draw()
    {
        mainWindow.SetInventoryCompanionVisible(inventoryCompanionWindow.UpdateAttachment());
        mainWindow.SetWritingLogVisible(writingLogWindow.IsOpen && playerState.IsLoaded);
        mainWindow.Tick(); windows.Draw();
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "compact": compactWindow.Toggle(); break;
            case "trade": mainWindow.OpenTradeFromLauncher(); break;
            case "writing": mainWindow.OpenWritingLogFromLauncher(); break;
            case "launcher":
                configuration.ShowLauncher = !configuration.ShowLauncher; launcherWindow.IsOpen = configuration.ShowLauncher;
                pluginInterface.SavePluginConfig(configuration); break;
            case "panels": ToggleAttachedInterfaces(); break;
            default: mainWindow.Toggle(); break;
        }
    }
    private void Open() => mainWindow.IsOpen = true;

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
