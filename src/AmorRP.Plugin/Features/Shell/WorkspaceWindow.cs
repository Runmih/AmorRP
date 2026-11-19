using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

public sealed class WorkspaceWindow : Window
{
    private readonly WorkspaceKind kind;
    private readonly Action<WorkspaceKind> draw;
    private readonly Func<bool> loaded;

    public WorkspaceWindow(WorkspaceKind kind, Action<WorkspaceKind> draw, Func<bool> loaded)
        : base($"Amor RP — {Title(kind)}###AmorRP{kind}Workspace")
    {
        this.kind = kind; this.draw = draw; this.loaded = loaded;
        Size = new Vector2(1040, 760); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(660, 480), MaximumSize = new(float.MaxValue) };
    }
    public static string Title(WorkspaceKind kind) => kind switch
    {
        WorkspaceKind.Crafting => "Crafting",
        WorkspaceKind.GroupManagement => "Group management",
        WorkspaceKind.CommunityManagement => "Season management",
        _ => "Season"
    };
    public override bool DrawConditions() => loaded();
    public override void Draw() => draw(kind);
}
