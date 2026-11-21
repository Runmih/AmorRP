using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AmorRP.Plugin.Services.Ui;

internal static class UiIcons
{
    public static void Draw(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(icon.ToIconString());
        ImGui.PopFont();
    }

    public static bool ClearButton(string label)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var clicked = ImGui.Button(FontAwesomeIcon.Times.ToIconString() + "###" + label);
        ImGui.PopFont();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(label);
        return clicked;
    }

    public static void SearchLabel()
    {
        ImGui.AlignTextToFramePadding();
        Draw(FontAwesomeIcon.Search);
        ImGui.SameLine();
    }
}
