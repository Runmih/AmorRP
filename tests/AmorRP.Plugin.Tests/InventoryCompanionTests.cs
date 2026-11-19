using System.Globalization;
using System.Numerics;
using AmorRP.Plugin.Features.Inventory;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class InventoryCompanionTests
{
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
