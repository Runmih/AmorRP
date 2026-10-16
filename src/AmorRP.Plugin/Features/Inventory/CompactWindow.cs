using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace AmorRP.Plugin.Features.Inventory;

public sealed class CompactWindow(Action draw) : Window("Amor RP consumables###AmorRPCompact")
{
    public override void Draw() => draw();
    public override void OnOpen() { Size = new Vector2(410, 440); SizeCondition = ImGuiCond.FirstUseEver; }
}
