using System.Numerics;
using AmorRP.Plugin.Features.Shell;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class LauncherDockTests
{
    [Theory]
    [InlineData(LauncherEdge.Top, false)]
    [InlineData(LauncherEdge.Bottom, true)]
    [InlineData(LauncherEdge.Left, true)]
    [InlineData(LauncherEdge.Right, false)]
    public void Opening_the_bar_keeps_the_handle_under_the_pointer_and_the_buttons_inside_the_viewport(LauncherEdge edge, bool atEnd)
    {
        var origin = new Vector2(120, 80);
        var viewport = new Vector2(320, 210);
        var position = atEnd ? 100 : 0;
        var hidden = LauncherDock.Place(edge, origin, viewport, position, 84, 1.5f, 8, false);
        var open = LauncherDock.Place(edge, origin, viewport, position, 84, 1.5f, 8, true);
        Assert.Equal(hidden.Position, open.Position + open.GripPosition);
        Assert.Equal(hidden.Size, open.GripSize);
        Assert.True(open.Position.X >= origin.X && open.Position.Y >= origin.Y);
        Assert.True(open.Position.X + open.Size.X <= origin.X + viewport.X + .001f);
        Assert.True(open.Position.Y + open.Size.Y <= origin.Y + viewport.Y + .001f);

        var horizontal = LauncherDock.IsHorizontal(edge);
        var lastButtonEnd = (horizontal ? open.ButtonsPosition.X : open.ButtonsPosition.Y) + 8 * open.ButtonSize + 7 * open.Gap;
        Assert.True(lastButtonEnd <= (horizontal ? open.Size.X : open.Size.Y) + .001f);
        switch (edge)
        {
            case LauncherEdge.Top: Assert.Equal(origin.Y, hidden.Position.Y); break;
            case LauncherEdge.Bottom: Assert.Equal(origin.Y + viewport.Y, hidden.Position.Y + hidden.Size.Y); break;
            case LauncherEdge.Left: Assert.Equal(origin.X, hidden.Position.X); break;
            case LauncherEdge.Right: Assert.Equal(origin.X + viewport.X, hidden.Position.X + hidden.Size.X); break;
        }
    }

    [Theory]
    [InlineData(LauncherEdge.Top)]
    [InlineData(LauncherEdge.Right)]
    public void Saved_position_measures_available_travel_in_either_orientation(LauncherEdge edge)
    {
        var viewport = new Vector2(1920, 1080);
        var start = LauncherDock.Place(edge, Vector2.Zero, viewport, 0, 42, 1, 6, true);
        var nearEnd = LauncherDock.Place(edge, Vector2.Zero, viewport, 75, 42, 1, 6, true);
        var end = LauncherDock.Place(edge, Vector2.Zero, viewport, 100, 42, 1, 6, true);
        Assert.Equal(Vector2.Lerp(start.Position, end.Position, .75f), nearEnd.Position);
    }
}
