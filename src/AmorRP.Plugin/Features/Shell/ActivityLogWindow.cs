using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

internal sealed class ActivityLogWindow : Window
{
    private readonly Action draw;

    public ActivityLogWindow(Action draw) : base("Amor RP — Activity Log###AmorRPActivityLog")
    {
        this.draw = draw;
        Size = new Vector2(560, 440);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() => draw();
}
