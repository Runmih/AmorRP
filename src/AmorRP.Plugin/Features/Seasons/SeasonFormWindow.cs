using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

// Season preparation has its own position, focus, size and open/close state.
public sealed class SeasonFormWindow(string title, Action draw, Func<bool> canDraw, Vector2 initialSize, Action opened) : Window(title)
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
