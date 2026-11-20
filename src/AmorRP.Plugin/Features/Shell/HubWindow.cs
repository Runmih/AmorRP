using System.Numerics;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Shell;

internal sealed class HubWindow : Window
{
    private static readonly (WorkspaceKind Page, FontAwesomeIcon Icon)[] Tabs =
    [
        (WorkspaceKind.Season, FontAwesomeIcon.Home),
        (WorkspaceKind.Inventory, FontAwesomeIcon.BoxOpen),
        (WorkspaceKind.Crafting, FontAwesomeIcon.Hammer),
        (WorkspaceKind.Writing, FontAwesomeIcon.BookOpen)
    ];

    private readonly Configuration configuration;
    private readonly Action<WorkspaceKind> draw;
    private readonly Func<bool> loaded;

    public HubWindow(Configuration configuration, Action<WorkspaceKind> draw, Func<bool> loaded)
        : base("###AmorRPSeasonWorkspace")
    {
        this.configuration = configuration;
        this.draw = draw;
        this.loaded = loaded;
        Size = new(1040, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(660, 480), MaximumSize = new(float.MaxValue) };
    }

    public WorkspaceKind Page { get; private set; } = WorkspaceKind.Season;
    public static bool Contains(WorkspaceKind page) => Tabs.Any(tab => tab.Page == page);
    public override bool DrawConditions() => loaded();

    public void Show(WorkspaceKind page)
    {
        Page = page;
        IsOpen = true;
        BringToFront();
    }

    public void TogglePage(WorkspaceKind page)
    {
        if (IsOpen && Page == page) IsOpen = false;
        else Show(page);
    }

    public override void Draw()
    {
        var edge = configuration.HubNavigationEdge;
        var horizontal = LauncherDock.IsHorizontal(edge);
        var button = ImGui.GetFrameHeight() * 1.4f;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var navSize = horizontal ? new Vector2(0, button) : new Vector2(button, 0);
        var contentSize = horizontal ? new Vector2(0, -button - spacing.Y) : new Vector2(-button - spacing.X, 0);

        if (edge is LauncherEdge.Bottom or LauncherEdge.Right)
        {
            DrawContent(contentSize);
            if (!horizontal) ImGui.SameLine();
            DrawTabs(navSize, button, horizontal);
        }
        else
        {
            DrawTabs(navSize, button, horizontal);
            if (!horizontal) ImGui.SameLine();
            DrawContent(Vector2.Zero);
        }
    }

    private void DrawContent(Vector2 size)
    {
        if (ImGui.BeginChild("HubContent", size, false)) draw(Page);
        ImGui.EndChild();
    }

    private void DrawTabs(Vector2 size, float button, bool horizontal)
    {
        if (ImGui.BeginChild("HubNavigation", size, false, ImGuiWindowFlags.NoScrollbar))
        {
            for (var i = 0; i < Tabs.Length; i++)
            {
                var (page, icon) = Tabs[i];
                if (i > 0 && horizontal) ImGui.SameLine();
                var selected = page == Page;
                if (selected) ImGui.PushStyleColor(ImGuiCol.Button, PluginTheme.SelectedButton);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(icon.ToIconString() + $"###Hub{page}", new Vector2(button))) Page = page;
                ImGui.PopFont();
                if (selected) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(WorkspaceWindow.Title(page));
            }
        }
        ImGui.EndChild();
    }
}
