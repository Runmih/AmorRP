using System.Numerics;

namespace AmorRP.Plugin.Features.Inventory;

// Game coordinates enter here in screen pixels, including the native UI scale.
internal readonly record struct InventoryAttachmentLayout(Vector2 Position, Vector2 Size)
{
    public static InventoryAttachmentLayout? Place(Vector2 inventoryPosition, Vector2 inventorySize,
        Vector2 viewportPosition, Vector2 viewportSize, float uiScale)
    {
        if (!float.IsFinite(uiScale) || uiScale <= 0 || !Valid(inventoryPosition) || !Valid(inventorySize)
            || !Valid(viewportPosition) || !Valid(viewportSize) || inventorySize.X <= 0 || inventorySize.Y <= 0
            || viewportSize.X <= 0 || viewportSize.Y <= 0) return null;

        var gap = 8 * uiScale;
        var right = viewportPosition.X + viewportSize.X;
        var bottom = viewportPosition.Y + viewportSize.Y;
        var x = Math.Max(viewportPosition.X, inventoryPosition.X + inventorySize.X + gap);
        var width = Math.Min(540 * uiScale, right - x - gap);
        var height = Math.Min(inventorySize.Y, viewportSize.Y - gap * 2);
        if (width < 100 * uiScale || height < 120 * uiScale) return null;
        var y = Math.Clamp(inventoryPosition.Y, viewportPosition.Y + gap, bottom - gap - height);
        return new(new(x, y), new(width, height));
    }

    private static bool Valid(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
