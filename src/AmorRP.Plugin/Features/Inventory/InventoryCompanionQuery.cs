using System.Globalization;
using System.Numerics;
using AmorRP.Core.Items;

namespace AmorRP.Plugin.Features.Inventory;

internal enum InventoryCompanionSection { All, Consumables, Writing, Supplies, Documents, Equipment }

// This browser has its own filters and pagination; the main menu can remain open.
internal sealed class InventoryCompanionQuery
{
    public InventoryCompanionSection Section { get; set; }
    public string Type { get; set; } = "";
    public string Search { get; set; } = "";
    public int Sort { get; set; }
    public bool CanLoad => IsSupported(Section);
    public static bool IsSupported(InventoryCompanionSection section) =>
        section is InventoryCompanionSection.All or InventoryCompanionSection.Consumables or InventoryCompanionSection.Writing or InventoryCompanionSection.Equipment;
    public string[] Types => Section switch
    {
        InventoryCompanionSection.All => CraftingTypes.All,
        InventoryCompanionSection.Consumables => CraftingTypes.Consumables,
        InventoryCompanionSection.Equipment => CraftingTypes.Blacksmithing,
        InventoryCompanionSection.Writing => CraftingTypes.Writing,
        _ => []
    };

    public string Build()
    {
        if (!CanLoad) throw new InvalidOperationException("This item family is not supported by the current server.");
        var query = "sort=" + (Sort switch { 1 => "type", 2 => "category", _ => "name" });
        if (Search.Trim().Length > 0) query += "&search=" + Uri.EscapeDataString(Search.Trim());
        if (Types.Contains(Type)) query += "&typeId=" + Type;
        else if (Section != InventoryCompanionSection.All) query += "&typeIds=" + string.Join(",", Types);
        return query;
    }

    public void Reset() { Section = InventoryCompanionSection.All; Type = Search = ""; Sort = 0; }

    public static string FormatNetBalance(string available, string? debt) =>
        BigInteger.TryParse(available, NumberStyles.None, CultureInfo.InvariantCulture, out var coins)
        && BigInteger.TryParse(debt, NumberStyles.None, CultureInfo.InvariantCulture, out var outstanding)
            ? (coins - outstanding).ToString("N0", CultureInfo.CurrentCulture) : "—";
}
