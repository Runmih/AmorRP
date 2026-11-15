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
    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.075f, .083f, .10f, .98f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.32f, .27f, .16f, .85f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.42f, .35f, .20f, .9f));
    }
    public override void PostDraw() => ImGui.PopStyleColor(3);
    public override void Draw() => draw();
}
