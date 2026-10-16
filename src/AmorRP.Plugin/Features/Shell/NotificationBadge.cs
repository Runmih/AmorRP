using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Shell;

public static class NotificationBadge
{
    public static void Draw()
    {
        var radius = ImGui.GetFontSize() * .28f;
        var max = ImGui.GetItemRectMax(); var min = ImGui.GetItemRectMin();
        var center = new Vector2(max.X - radius - 2, min.Y + radius + 2);
        var draw = ImGui.GetWindowDrawList();
        draw.AddCircleFilled(center, radius + 1.5f, ImGui.ColorConvertFloat4ToU32(new Vector4(.12f, .1f, .16f, 1)));
        draw.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(.95f, .23f, .3f, 1)));
    }
}
