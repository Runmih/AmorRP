using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace AmorRP.Plugin.Features.Shell;

public sealed class FavoriteEventsWindow : Window
{
    private readonly Action draw, closed;
    private readonly Func<bool> loaded;
    public FavoriteEventsWindow(Action draw, Func<bool> loaded, Action closed) : base("Amor RP — Favorite events###AmorRPFavoriteEvents")
    { this.draw = draw; this.loaded = loaded; this.closed = closed; Size = new Vector2(360, 260); SizeCondition = ImGuiCond.FirstUseEver; }
    public override bool DrawConditions() => loaded();
    public override void Draw() => draw();
    public override void OnClose() => closed();
}
