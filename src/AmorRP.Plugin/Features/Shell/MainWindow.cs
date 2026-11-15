using System.Numerics;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Shell;

public sealed class MainWindow : Window, IDisposable
{
    private readonly AmorRP.Plugin.Features.Seasons.SeasonsPanel communities;

    public MainWindow(IPlayerState playerState, IPartyList party, Configuration configuration, Action save, ITextureProvider textures)
        : base("Amor RP — Settings###AmorRPMain")
    {
        communities = new(playerState, configuration, save, party, textures);
        Size = new Vector2(660, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(460, 360), MaximumSize = new(float.MaxValue) };
        communities.OpenSettingsWindow = () => IsOpen = true;
        communities.OpenWritingLogWindow = () => OpenWritingLog?.Invoke();
        communities.OpenWorkspaceWindow = kind => OpenWorkspace?.Invoke(kind);
        communities.OpenTradeWindow = () => OpenTrade?.Invoke();
        communities.OpenCreateCommunity = () => OpenCreateCommunity?.Invoke();
        communities.CloseCreateCommunity = () => CloseCreateCommunity?.Invoke();
        communities.SetFavoriteEventsVisible = visible => SetFavoriteEventsVisible?.Invoke(visible);
        communities.SetLauncherVisible = visible => SetLauncherVisible?.Invoke(visible);
    }

    public void Tick() { communities.EnsureScope(ProductServer.Origin); communities.Tick(); }
    public int PendingTradeRequests => communities.PendingTradeRequests;
    public int UnreadAnnouncements => communities.UnreadAnnouncements;
    public bool CanOpenGroupManagement => communities.CanOpenGroupManagement;
    public bool CanOpenCommunityManagement => communities.CanOpenCommunityManagement;
    public Action<WorkspaceKind>? OpenWorkspace { get; set; }
    public void OpenWorkspaceFromLauncher(WorkspaceKind kind) => communities.OpenWorkspace(kind);
    public void DrawWorkspace(WorkspaceKind kind) => communities.Draw(ProductServer.Origin, workspace: kind);
    public void OpenWritingLogFromLauncher() => communities.OpenWritingLog();
    public void DrawWritingLog() => communities.Draw(ProductServer.Origin, writingMode: true);
    public void SetWritingLogVisible(bool visible) => communities.SetWritingLogVisible(visible);
    public Action? OpenWritingLog { get; set; }
    public void OpenTradeFromLauncher() => communities.OpenTradeFromLauncher();
    public void DrawFavoriteEvents() => communities.DrawFavoriteEvents();
    public Action<bool>? SetFavoriteEventsVisible { get; set; }
    public Action<bool>? SetLauncherVisible { get; set; }
    public Action? OpenTrade { get; set; }
    public void DrawTrade() => communities.DrawTradeWindow();
    public void TradeOpened() => communities.TradeWindowOpened();
    public void TradeClosed() => communities.TradeWindowClosed();
    public Action? OpenCreateCommunity { get; set; }
    public Action? CloseCreateCommunity { get; set; }
    public bool CanDrawCommunityForms => communities.CanDrawCommunityForms;
    public void DrawCreateCommunity() => communities.DrawCreateCommunity();
    public void DrawCompact() => communities.Draw(ProductServer.Origin, compactMode: true);
    public void DrawInventoryCompanion() => communities.Draw(ProductServer.Origin, inventoryMode: true);
    public void SetInventoryCompanionVisible(bool visible) => communities.SetInventoryCompanionVisible(visible);
    public override void Draw() => communities.Draw(ProductServer.Origin);
    public void ClearFeedback() => communities.WindowOpened();
    public override void OnOpen() => ClearFeedback();
    public void SelectTarget(CharacterTarget target) => communities.SelectTradeTarget(target);
    public void Dispose() => communities.Dispose();
}
