using System.Numerics;

namespace AmorRP.Plugin.Features.Shell;

public enum LauncherEdge { Top, Bottom, Left, Right }

internal readonly record struct LauncherDock(
    Vector2 Position, Vector2 Size, Vector2 GripPosition, Vector2 GripSize,
    Vector2 ButtonsPosition, float ButtonSize, float Gap, float Travel)
{
    public static bool IsHorizontal(LauncherEdge edge) => edge is LauncherEdge.Top or LauncherEdge.Bottom;

    public static float ClampPercent(float value, float minimum, float maximum, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    public static LauncherDock Place(LauncherEdge edge, Vector2 origin, Vector2 viewport, float position,
        float buttonSize, float scale, int buttonCount, bool expanded)
    {
        var horizontal = IsHorizontal(edge);
        var span = Math.Max(1, horizontal ? viewport.X : viewport.Y);
        var depth = Math.Max(1, horizontal ? viewport.Y : viewport.X);
        var gripDepth = Math.Min(10 * scale, depth / 3);
        var gap = Math.Min(5 * scale, span / (buttonCount * 3));
        var padding = Math.Min(gap, Math.Max(0, (depth - gripDepth) / 4));
        var button = Math.Min(buttonSize, Math.Min((span - 2 * padding - gap * (buttonCount - 1)) / buttonCount,
            depth - gripDepth - 2 * padding));
        button = Math.Max(float.Epsilon, button);
        var length = Math.Min(span, buttonCount * button + (buttonCount - 1) * gap + 2 * padding);
        var travel = span - length;
        var along = travel * ClampPercent(position, 0, 100, 50) / 100;
        var gripLength = Math.Min(52 * scale, length);
        var gripStart = (length - gripLength) / 2;
        var barDepth = gripDepth + button + 2 * padding;
        var farEdge = edge is LauncherEdge.Bottom or LauncherEdge.Right;

        Vector2 Project(float a, float d) => horizontal ? new(a, d) : new(d, a);
        if (!expanded)
            return new(origin + Project(along + gripStart, farEdge ? depth - gripDepth : 0),
                Project(gripLength, gripDepth), Vector2.Zero, Project(gripLength, gripDepth), Vector2.Zero, button, gap, travel);

        return new(origin + Project(along, farEdge ? depth - barDepth : 0), Project(length, barDepth),
            Project(gripStart, farEdge ? barDepth - gripDepth : 0), Project(gripLength, gripDepth),
            Project(padding, padding + (farEdge ? 0 : gripDepth)), button, gap, travel);
    }
}
