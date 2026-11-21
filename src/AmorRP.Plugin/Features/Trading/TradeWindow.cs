using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace AmorRP.Plugin.Features.Trading;

public sealed class TradeWindow(Action draw, Action opened, Action closed) : Window("Amor RP trade###AmorRPTrade")
{
    public override void Draw() => draw();
    public override void OnOpen() { Size = new Vector2(960, 760); SizeCondition = ImGuiCond.FirstUseEver; opened(); }
    public override void OnClose() => closed();
}

public sealed class TradeRequestWindow(Action draw) : Window("Trade request###AmorRPTradeRequest",
    ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
{
    public override void Draw() => draw();
}
