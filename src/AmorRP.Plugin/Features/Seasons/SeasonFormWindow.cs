using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed class SeasonFormWindow(string title, Action draw, Func<bool> canDraw, Vector2 initialSize, Action opened, Action? closed = null) : Window(title)
{
    public override bool DrawConditions() => canDraw();
    public override void Draw() => draw();
    public override void OnClose() => closed?.Invoke();
    public override void OnOpen()
    {
        opened();
        Size = initialSize;
        SizeCondition = ImGuiCond.FirstUseEver;
    }
}
