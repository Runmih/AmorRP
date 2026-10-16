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
    private readonly WindowSystem windows = new("AmorRP");
    private readonly Configuration configuration;
    private readonly LauncherWindow launcherWindow;
    private readonly MainWindow mainWindow;
    private readonly AmorRP.Plugin.Features.Inventory.CompactWindow compactWindow;
    private readonly AmorRP.Plugin.Features.Trading.TradeWindow tradeWindow;
    private readonly AmorRP.Plugin.Features.Groups.GroupFormWindow joinGroupWindow, createGroupWindow;
    private readonly CharacterContext context;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPlayerState playerState,
        IContextMenu contextMenu, IPartyList party, ITextureProvider textures)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (configuration.Version < 4) { configuration.Version = 4; pluginInterface.SavePluginConfig(configuration); }
        mainWindow = new MainWindow(playerState, party, configuration, () => pluginInterface.SavePluginConfig(configuration), textures);
        compactWindow = new(mainWindow.DrawCompact);
        tradeWindow = new(mainWindow.DrawTrade, mainWindow.TradeOpened, mainWindow.TradeClosed);
        mainWindow.OpenTrade = () => tradeWindow.IsOpen = true;
        joinGroupWindow = new("Amor RP — Join group###AmorRPJoinGroup", mainWindow.DrawJoinGroup, () => mainWindow.CanDrawGroupForms, new(520, 360), mainWindow.ClearFeedback);
        createGroupWindow = new("Amor RP — Create group###AmorRPCreateGroup", mainWindow.DrawCreateGroup, () => mainWindow.CanDrawGroupForms, new(560, 660), mainWindow.ClearFeedback);
        mainWindow.OpenJoinGroup = () => joinGroupWindow.IsOpen = true;
        mainWindow.OpenCreateGroup = () => createGroupWindow.IsOpen = true;
        mainWindow.CloseJoinGroup = () => joinGroupWindow.IsOpen = false;
        mainWindow.CloseCreateGroup = () => createGroupWindow.IsOpen = false;
        launcherWindow = new(mainWindow.OpenMainFromLauncher, mainWindow.OpenTradeFromLauncher,
            () => mainWindow.PendingTradeRequests, () => mainWindow.UnreadAnnouncements, () => playerState.IsLoaded, mainWindow.DrawLauncherGroupSelector)
            { IsOpen = configuration.ShowLauncher };
        mainWindow.SetLauncherVisible = visible => launcherWindow.IsOpen = visible;
        context = new(contextMenu, mainWindow.SelectTarget);
        windows.AddWindow(mainWindow);
        windows.AddWindow(compactWindow);
        windows.AddWindow(tradeWindow);
        windows.AddWindow(joinGroupWindow);
        windows.AddWindow(createGroupWindow);
        windows.AddWindow(launcherWindow);
        commands.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "/amorrp → Main Menu" });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        pluginInterface.UiBuilder.OpenConfigUi += Open;
    }

    private void Draw() { mainWindow.Tick(); windows.Draw(); }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "compact": compactWindow.Toggle(); break;
            case "trade": mainWindow.OpenTradeFromLauncher(); break;
            case "launcher":
                configuration.ShowLauncher = !configuration.ShowLauncher; launcherWindow.IsOpen = configuration.ShowLauncher;
                pluginInterface.SavePluginConfig(configuration); break;
            default: mainWindow.Toggle(); break;
        }
    }
    private void Open() => mainWindow.IsOpen = true;

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
