namespace AmorRP.Core.Items;

// Item families are independent of external API versions and wire addresses.
public static class CraftingTypes
{
    public static readonly string[] Alchemy = ["potion", "poison", "explosive", "drug", "medicine"];
    public static readonly string[] Cooking = ["meal", "drink", "dessert"];
    public static readonly string[] Writing = ["book", "letter", "note"];
    public static readonly string[] Consumables = [..Alchemy, ..Cooking];
    public static readonly string[] All = [..Consumables, ..Writing];
    public static bool IsConsumable(string type) => Consumables.Contains(type);
    public static bool IsCooking(string type) => Cooking.Contains(type);
    public static string CraftPermission(string type) => IsCooking(type) ? "items.cooking.create" : "items.potion.create";
    public static string RecipePermission(string type) => IsCooking(type) ? "items.cooking.define" : "items.potion.define";
    public static string QuotaKind(string type) => IsCooking(type) ? "cooking_points" : "potion_points";
}
