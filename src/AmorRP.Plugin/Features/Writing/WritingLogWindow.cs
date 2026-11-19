using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Writing;

public sealed class WritingLogWindow : Window
{
    private readonly Action draw;
    private readonly Func<bool> canDraw;

    public WritingLogWindow(Action draw, Func<bool> canDraw) : base("Amor RP — Writing log###AmorRPWritingLog")
    {
        this.draw = draw; this.canDraw = canDraw;
        Size = new Vector2(1000, 740); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(620, 460), MaximumSize = new(float.MaxValue) };
    }

    public override bool DrawConditions() => canDraw();
    public override void Draw() => draw();
}
