using System.Numerics;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Shell;

public sealed class MainWindow : Window, IDisposable
{
    private readonly AmorRP.Plugin.Features.Groups.GroupsPanel groups;

    public MainWindow(IPlayerState playerState, IPartyList party, Configuration configuration, Action save, ITextureProvider textures)
        : base("Amor RP###AmorRPMain")
    {
        groups = new(playerState, configuration, save, party, textures);
        Size = new Vector2(960, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        groups.OpenMainWindow = () => IsOpen = true;
        groups.OpenTradeWindow = () => OpenTrade?.Invoke();
        groups.OpenJoinGroup = () => OpenJoinGroup?.Invoke();
        groups.OpenCreateGroup = () => OpenCreateGroup?.Invoke();
        groups.CloseJoinGroup = () => CloseJoinGroup?.Invoke();
        groups.CloseCreateGroup = () => CloseCreateGroup?.Invoke();
        groups.SetLauncherVisible = visible => SetLauncherVisible?.Invoke(visible);
    }

    public void Tick() { groups.EnsureScope(ProductServer.Origin); groups.Tick(); }
    public int PendingTradeRequests => groups.PendingTradeRequests;
    public int UnreadAnnouncements => groups.UnreadAnnouncements;
    public void OpenMainFromLauncher() { groups.FocusUnreadAnnouncements(); IsOpen = true; }
    public void DrawLauncherGroupSelector() => groups.DrawLauncherGroupSelector();
    public void OpenTradeFromLauncher() => groups.OpenTradeFromLauncher();
    public Action<bool>? SetLauncherVisible { get; set; }
    public Action? OpenTrade { get; set; }
    public void DrawTrade() => groups.DrawTradeWindow();
    public void TradeOpened() => groups.TradeWindowOpened();
    public void TradeClosed() => groups.TradeWindowClosed();
    public Action? OpenJoinGroup { get; set; }
    public Action? OpenCreateGroup { get; set; }
    public Action? CloseJoinGroup { get; set; }
    public Action? CloseCreateGroup { get; set; }
    public bool CanDrawGroupForms => groups.CanDrawGroupForms;
    public void DrawJoinGroup() => groups.DrawJoinGroup();
    public void DrawCreateGroup() => groups.DrawCreateGroup();
    public void DrawCompact() => groups.Draw(ProductServer.Origin, compactMode: true);
    public override void Draw() => groups.Draw(ProductServer.Origin);
    public void ClearFeedback() => groups.WindowOpened();
    public override void OnOpen() => ClearFeedback();
    public void SelectTarget(CharacterTarget target) => groups.SelectTradeTarget(target);
    public void Dispose() => groups.Dispose();
}
