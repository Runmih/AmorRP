using System.Numerics;

namespace AmorRP.Plugin.Features.Shell;

public enum LauncherEdge { Top, Bottom, Left, Right }

internal readonly record struct LauncherDock(
    Vector2 Position, Vector2 Size, Vector2 GripPosition, Vector2 GripSize,
    Vector2 ButtonsPosition, float ButtonSize, float Gap, float Travel, float VerticalTravel = 0)
{
    public static bool IsHorizontal(LauncherEdge edge) => edge is LauncherEdge.Top or LauncherEdge.Bottom;

    public static float ClampPercent(float value, float minimum, float maximum, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    public static LauncherDock Place(LauncherEdge edge, Vector2 origin, Vector2 viewport, float position,
        float buttonSize, float scale, int buttonCount, bool expanded, bool docked = true, Vector2 floatingPosition = default)
    {
        var horizontal = IsHorizontal(edge);
        var span = Math.Max(1, horizontal ? viewport.X : viewport.Y);
        var depth = Math.Max(1, horizontal ? viewport.Y : viewport.X);
        var gripDepth = Math.Min(10 * scale, depth / 3);
        if (!docked && !horizontal) span -= Math.Min(10 * scale, span / 3);
        var gap = Math.Min(5 * scale, span / (buttonCount * 3));
        var padding = Math.Min(gap, Math.Max(0, (depth - gripDepth) / 4));
        var button = Math.Min(buttonSize, Math.Min((span - 2 * padding - gap * (buttonCount - 1)) / buttonCount,
            depth - gripDepth - 2 * padding));
        button = Math.Max(float.Epsilon, button);
        var length = Math.Min(span, buttonCount * button + (buttonCount - 1) * gap + 2 * padding);
        var travel = span - length;
        var along = travel * ClampPercent(position, 0, 100, 50) / 100;
        if (!docked)
        {
            var content = horizontal
                ? new Vector2(length - 2 * padding, button)
                : new Vector2(button, length - 2 * padding);
            return PlacePanel(LauncherEdge.Top, origin, viewport, position, content, scale, true, false, floatingPosition, padding)
                with { ButtonSize = button, Gap = gap };
        }
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

    public static LauncherDock PlacePanel(LauncherEdge edge, Vector2 origin, Vector2 viewport, float position,
        Vector2 content, float scale, bool expanded, bool docked, Vector2 floatingPosition, float padding)
    {
        var horizontal = !docked || IsHorizontal(edge);
        var gripDepth = Math.Min(10 * scale, Math.Max(1, horizontal ? viewport.Y : viewport.X) / 3);
        var gripBand = horizontal ? new Vector2(0, gripDepth) : new Vector2(gripDepth, 0);
        var bounds = Vector2.Max(Vector2.One, viewport);
        var size = Vector2.Min(bounds, content + gripBand + new Vector2(2 * padding));
        var available = bounds - size;
        var gripLength = Math.Min(52 * scale, horizontal ? size.X : size.Y);
        var far = docked && (edge is LauncherEdge.Bottom or LauncherEdge.Right);
        var gripSize = horizontal ? new Vector2(gripLength, gripDepth) : new Vector2(gripDepth, gripLength);
        var grip = horizontal ? new Vector2((size.X - gripLength) / 2, far ? size.Y - gripDepth : 0)
            : new Vector2(far ? size.X - gripDepth : 0, (size.Y - gripLength) / 2);
        var along = ClampPercent(position, 0, 100, 50) / 100;
        var offset = docked
            ? horizontal ? new Vector2(available.X * along, far ? available.Y : 0)
                         : new Vector2(far ? available.X : 0, available.Y * along)
            : available * new Vector2(ClampPercent(floatingPosition.X, 0, 100, 10), ClampPercent(floatingPosition.Y, 0, 100, 30)) / 100;
        var travel = horizontal ? available.X : available.Y;
        if (docked && !expanded)
            return new(origin + offset + grip, gripSize, Vector2.Zero, gripSize, Vector2.Zero, 0, 0, travel);
        return new(origin + offset, size, grip, gripSize, new Vector2(padding) + (far ? Vector2.Zero : gripBand),
            0, 0, travel, available.Y);
    }
}
