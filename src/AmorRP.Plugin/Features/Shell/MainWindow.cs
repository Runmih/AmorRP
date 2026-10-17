using System.Numerics;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Shell;

public sealed class MainWindow : Window, IDisposable
{
    private readonly AmorRP.Plugin.Features.Communities.CommunitiesPanel communities;

    public MainWindow(IPlayerState playerState, IPartyList party, Configuration configuration, Action save, ITextureProvider textures)
        : base("Amor RP###AmorRPMain")
    {
        communities = new(playerState, configuration, save, party, textures);
        Size = new Vector2(960, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        communities.OpenMainWindow = () => IsOpen = true;
        communities.OpenTradeWindow = () => OpenTrade?.Invoke();
        communities.OpenJoinCommunity = () => OpenJoinCommunity?.Invoke();
        communities.OpenCreateCommunity = () => OpenCreateCommunity?.Invoke();
        communities.CloseJoinCommunity = () => CloseJoinCommunity?.Invoke();
        communities.CloseCreateCommunity = () => CloseCreateCommunity?.Invoke();
        communities.SetLauncherVisible = visible => SetLauncherVisible?.Invoke(visible);
    }

    public void Tick() { communities.EnsureScope(ProductServer.Origin); communities.Tick(); }
    public int PendingTradeRequests => communities.PendingTradeRequests;
    public int UnreadAnnouncements => communities.UnreadAnnouncements;
    public void OpenMainFromLauncher() { communities.FocusUnreadAnnouncements(); IsOpen = true; }
    public void DrawLauncherCommunitySelector() => communities.DrawLauncherCommunitySelector();
    public void OpenTradeFromLauncher() => communities.OpenTradeFromLauncher();
    public Action<bool>? SetLauncherVisible { get; set; }
    public Action? OpenTrade { get; set; }
    public void DrawTrade() => communities.DrawTradeWindow();
    public void TradeOpened() => communities.TradeWindowOpened();
    public void TradeClosed() => communities.TradeWindowClosed();
    public Action? OpenJoinCommunity { get; set; }
    public Action? OpenCreateCommunity { get; set; }
    public Action? CloseJoinCommunity { get; set; }
    public Action? CloseCreateCommunity { get; set; }
    public bool CanDrawCommunityForms => communities.CanDrawCommunityForms;
    public void DrawJoinCommunity() => communities.DrawJoinCommunity();
    public void DrawCreateCommunity() => communities.DrawCreateCommunity();
    public void DrawCompact() => communities.Draw(ProductServer.Origin, compactMode: true);
    public override void Draw() => communities.Draw(ProductServer.Origin);
    public void ClearFeedback() => communities.WindowOpened();
    public override void OnOpen() => ClearFeedback();
    public void SelectTarget(CharacterTarget target) => communities.SelectTradeTarget(target);
    public void Dispose() => communities.Dispose();
}
