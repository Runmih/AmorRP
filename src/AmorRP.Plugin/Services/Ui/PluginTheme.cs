using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AmorRP.Plugin.Services.Ui;

// Scoped to our WindowSystem, so other Dalamud plugins retain their own style.
internal static class PluginTheme
{
    public static readonly Vector4 Accent = new(.88f, .75f, .47f, 1);
    public static readonly Vector4 SelectedButton = new(.32f, .27f, .16f, 1);
    private static readonly (ImGuiCol Key, Vector4 Color)[] Colors =
    [
        (ImGuiCol.Text, new(.92f, .91f, .87f, 1)),
        (ImGuiCol.TextDisabled, new(.56f, .58f, .60f, 1)),
        (ImGuiCol.WindowBg, new(.075f, .083f, .10f, .98f)),
        (ImGuiCol.ChildBg, new(.065f, .073f, .09f, .98f)),
        (ImGuiCol.PopupBg, new(.10f, .11f, .13f, .98f)),
        (ImGuiCol.Border, new(.48f, .40f, .25f, .8f)),
        (ImGuiCol.TitleBg, new(.08f, .09f, .11f, 1)),
        (ImGuiCol.TitleBgActive, new(.19f, .17f, .13f, 1)),
        (ImGuiCol.TitleBgCollapsed, new(.08f, .09f, .11f, .95f)),
        (ImGuiCol.FrameBg, new(.12f, .13f, .15f, 1)),
        (ImGuiCol.FrameBgHovered, new(.20f, .21f, .23f, 1)),
        (ImGuiCol.FrameBgActive, new(.27f, .24f, .17f, 1)),
        (ImGuiCol.Button, new(.22f, .23f, .25f, 1)),
        (ImGuiCol.ButtonHovered, new(.38f, .32f, .20f, 1)),
        (ImGuiCol.ButtonActive, new(.49f, .40f, .22f, 1)),
        (ImGuiCol.Header, new(.32f, .27f, .16f, .85f)),
        (ImGuiCol.HeaderHovered, new(.42f, .35f, .20f, .9f)),
        (ImGuiCol.HeaderActive, new(.49f, .40f, .22f, 1)),
        (ImGuiCol.Tab, new(.12f, .13f, .15f, 1)),
        (ImGuiCol.TabActive, SelectedButton),
        (ImGuiCol.TabHovered, new(.42f, .35f, .20f, 1)),
        (ImGuiCol.TabUnfocused, new(.10f, .11f, .13f, 1)),
        (ImGuiCol.TabUnfocusedActive, new(.22f, .20f, .15f, 1)),
        (ImGuiCol.CheckMark, Accent),
        (ImGuiCol.SliderGrab, new(.66f, .55f, .33f, 1)),
        (ImGuiCol.SliderGrabActive, Accent),
        (ImGuiCol.Separator, new(.34f, .31f, .24f, .7f)),
        (ImGuiCol.SeparatorHovered, new(.70f, .58f, .32f, 1)),
        (ImGuiCol.SeparatorActive, Accent),
        (ImGuiCol.ResizeGrip, new(.32f, .27f, .16f, .35f)),
        (ImGuiCol.ResizeGripHovered, new(.42f, .35f, .20f, .75f)),
        (ImGuiCol.ResizeGripActive, Accent),
        (ImGuiCol.ScrollbarBg, new(.06f, .07f, .09f, .85f)),
        (ImGuiCol.ScrollbarGrab, new(.27f, .28f, .30f, 1)),
        (ImGuiCol.ScrollbarGrabHovered, new(.38f, .35f, .27f, 1)),
        (ImGuiCol.ScrollbarGrabActive, new(.49f, .40f, .22f, 1)),
    ];
    private static readonly (ImGuiStyleVar Key, float Value)[] Rounding =
    [
        (ImGuiStyleVar.WindowRounding, 8),
        (ImGuiStyleVar.FrameRounding, 4),
        (ImGuiStyleVar.PopupRounding, 6),
        (ImGuiStyleVar.TabRounding, 4),
        (ImGuiStyleVar.GrabRounding, 3),
        (ImGuiStyleVar.ScrollbarRounding, 6),
    ];

    public static Scope Apply()
    {
        foreach (var (key, color) in Colors) ImGui.PushStyleColor(key, color);
        foreach (var (key, value) in Rounding) ImGui.PushStyleVar(key, value * ImGuiHelpers.GlobalScale);
        return new Scope();
    }

    public readonly struct Scope : IDisposable
    {
        public void Dispose()
        {
            ImGui.PopStyleVar(Rounding.Length);
            ImGui.PopStyleColor(Colors.Length);
        }
    }
}
