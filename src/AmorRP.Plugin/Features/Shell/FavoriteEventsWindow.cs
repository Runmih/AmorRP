using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace AmorRP.Plugin.Features.Shell;

public sealed class FavoriteEventsWindow : Window
{
    private readonly Action draw;
    private readonly Func<bool> loaded;
    public FavoriteEventsWindow(Action draw, Func<bool> loaded)
        : base("Amor RP — Favorite events###AmorRPFavoriteEvents", ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.draw = draw;
        this.loaded = loaded;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Position = new Vector2(24, 260);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(340, 0), MaximumSize = new(340, float.MaxValue) };
    }
    public override bool DrawConditions() => loaded();
    public override void Draw() => draw();
}
