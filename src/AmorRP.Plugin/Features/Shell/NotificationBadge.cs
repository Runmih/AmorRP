using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Shell;

public static class NotificationBadge
{
    private static readonly Vector4 TradeColor = new(.95f, .23f, .3f, 1);
    private static readonly Vector4 AnnouncementColor = new(.36f, .72f, 1, 1);

    public static void Draw(bool announcement = false)
    {
        var radius = ImGui.GetFontSize() * .28f;
        var max = ImGui.GetItemRectMax(); var min = ImGui.GetItemRectMin();
        var center = new Vector2(max.X - radius - 2, min.Y + radius + 2);
        DrawAt(center, radius, announcement ? AnnouncementColor : TradeColor);
    }

    internal static void DrawOnHandle(LauncherDock dock, bool horizontal, bool hasTrade, bool hasAnnouncements)
    {
        var center = dock.Position + dock.GripPosition + dock.GripSize / 2;
        var radius = Math.Min(dock.GripSize.X, dock.GripSize.Y) * .32f;
        var offset = horizontal ? new Vector2(dock.GripSize.X * .38f, 0) : new Vector2(0, dock.GripSize.Y * .38f);
        if (hasTrade) DrawAt(center - offset, radius, TradeColor);
        if (hasAnnouncements) DrawAt(center + offset, radius, AnnouncementColor);
    }

    private static void DrawAt(Vector2 center, float radius, Vector4 color)
    {
        var draw = ImGui.GetWindowDrawList();
        draw.AddCircleFilled(center, radius * 1.3f, ImGui.GetColorU32(new Vector4(.12f, .1f, .16f, 1)));
        draw.AddCircleFilled(center, radius, ImGui.GetColorU32(color));
    }
}
