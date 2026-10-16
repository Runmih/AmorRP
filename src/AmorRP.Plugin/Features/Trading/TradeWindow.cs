using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace AmorRP.Plugin.Features.Trading;

public sealed class TradeWindow(Action draw, Action opened, Action closed) : Window("Amor RP trade###AmorRPTrade")
{
    public override void Draw() => draw();
    public override void OnOpen() { Size = new Vector2(900, 650); SizeCondition = ImGuiCond.FirstUseEver; opened(); }
    public override void OnClose() => closed();
}
