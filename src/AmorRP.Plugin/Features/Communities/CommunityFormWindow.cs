using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Communities;

// Separate windows give Join and Create independent position,
// focus, size and open/close state while sharing the verified character context.
public sealed class CommunityFormWindow(string title, Action draw, Func<bool> canDraw, Vector2 initialSize, Action opened) : Window(title)
{
    public override bool DrawConditions() => canDraw();
    public override void Draw() => draw();
    public override void OnOpen()
    {
        opened();
        Size = initialSize;
        SizeCondition = ImGuiCond.FirstUseEver;
    }
}
