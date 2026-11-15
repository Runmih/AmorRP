using System.Globalization;
using System.Numerics;
using AmorRP.Plugin.Features.Inventory;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class InventoryCompanionTests
{
    [Fact]
    public void Attachment_tracks_the_native_right_edge_and_height_in_screen_pixels()
    {
        var normal = InventoryAttachmentLayout.Place(new(50, 20), new(460, 800), Vector2.Zero, new(1920, 1080), 1)!.Value;
        Assert.Equal(new Vector2(518, 20), normal.Position);
        Assert.Equal(new Vector2(540, 800), normal.Size);

        var scaled = InventoryAttachmentLayout.Place(new(120, 70), new(690, 900), Vector2.Zero, new(2560, 1440), 1.5f)!.Value;
        Assert.Equal(new Vector2(822, 70), scaled.Position);
        Assert.Equal(new Vector2(810, 900), scaled.Size);
    }

    [Fact]
    public void Limited_space_shrinks_the_panel_without_moving_it_over_inventory_or_to_the_left()
    {
        var panel = InventoryAttachmentLayout.Place(new(1000, 700), new(460, 800), Vector2.Zero, new(1920, 1080), 1)!.Value;
        Assert.Equal(1468, panel.Position.X);
        Assert.Equal(444, panel.Size.X);
        Assert.True(panel.Position.Y >= 0);
        Assert.True(panel.Position.Y + panel.Size.Y <= 1080);
        Assert.Null(InventoryAttachmentLayout.Place(new(1500, 20), new(460, 800), Vector2.Zero, new(1920, 1080), 1));
    }

    [Fact]
    public void Attachment_respects_viewport_offsets_and_rejects_unusable_native_bounds()
    {
        var panel = InventoryAttachmentLayout.Place(new(2050, 50), new(460, 800), new(1920, 0), new(1920, 1080), 1)!.Value;
        Assert.Equal(2518, panel.Position.X);
        Assert.Null(InventoryAttachmentLayout.Place(Vector2.Zero, Vector2.Zero, Vector2.Zero, new(1920, 1080), 1));
        Assert.Null(InventoryAttachmentLayout.Place(new(float.NaN, 0), new(460, 800), Vector2.Zero, new(1920, 1080), 1));
        Assert.Null(InventoryAttachmentLayout.Place(Vector2.Zero, new(460, 800), Vector2.Zero, new(1920, 1080), float.PositiveInfinity));
    }

    [Fact]
    public void Search_and_sort_are_applied_on_the_server_before_pagination()
    {
        var query = new InventoryCompanionQuery { Search = " Tea & cake? ", Sort = 2 };
        Assert.Equal("sort=category&search=Tea%20%26%20cake%3F", query.Build());
        query.Section = InventoryCompanionSection.Writing;
        query.Type = "book";
        Assert.Equal("sort=category&search=Tea%20%26%20cake%3F&typeId=book", query.Build());
    }

    [Fact]
    public void Switching_families_cannot_send_a_stale_incompatible_type_or_an_unsupported_family()
    {
        var query = new InventoryCompanionQuery { Section = InventoryCompanionSection.Writing, Type = "meal" };
        Assert.Equal("sort=name&typeIds=book,letter,note", query.Build());
        query.Section = InventoryCompanionSection.Consumables;
        Assert.Contains("typeId=meal", query.Build());
        Assert.DoesNotContain("book", query.Types);
        foreach (var section in new[] { InventoryCompanionSection.Supplies, InventoryCompanionSection.Documents })
        {
            query.Section = section;
            Assert.False(query.CanLoad);
            Assert.Throws<InvalidOperationException>(() => query.Build());
        }
        query.Reset();
        Assert.Equal("sort=name", query.Build());
    }

    [Fact]
    public void Financial_totals_preserve_whole_units_beyond_a_single_wallet_and_never_invent_zero_for_unknown_data()
    {
        var expected = BigInteger.Parse("18446744073709551614", CultureInfo.InvariantCulture).ToString("N0", CultureInfo.CurrentCulture);
        Assert.Equal(expected, InventoryCompanionQuery.FormatAmount("18446744073709551614"));
        Assert.Equal("0", InventoryCompanionQuery.FormatAmount("0"));
        Assert.Equal("—", InventoryCompanionQuery.FormatAmount(null));
        Assert.Equal("—", InventoryCompanionQuery.FormatAmount("not loaded"));
    }
}
