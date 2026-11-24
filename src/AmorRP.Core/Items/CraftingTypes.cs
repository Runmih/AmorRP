namespace AmorRP.Core.Items;

public enum CraftingDiscipline { Alchemy, Cooking, Blacksmithing }

// Item families are independent of external API versions and wire addresses.
public static class CraftingTypes
{
    public const string GatherPermission = "items.gather.create";
    public const string BlacksmithPermission = "items.blacksmith.create";
    public const string BlacksmithRecipePermission = "items.blacksmith.define";
    public const string AlchemySupplies = "alchemy-supplies";
    public const string CookingSupplies = "cooking-supplies";
    public static readonly string[] Alchemy = ["potion", "poison", "explosive", "drug", "medicine"];
    public static readonly string[] Cooking = ["meal", "drink", "dessert"];
    public static readonly string[] Blacksmithing = ["weapon", "armor"];
    public static readonly string[] Writing = ["book", "letter", "note"];
    public static readonly string[] Consumables = [..Alchemy, ..Cooking, AlchemySupplies, CookingSupplies];
    public static readonly string[] Stackable = [..Consumables, ..Blacksmithing];
    public static readonly string[] All = [..Stackable, ..Writing];

    public static string[] Types(CraftingDiscipline discipline) => discipline switch
    {
        CraftingDiscipline.Cooking => Cooking,
        CraftingDiscipline.Blacksmithing => Blacksmithing,
        _ => Alchemy
    };

    public static bool IsConsumable(string type) => Consumables.Contains(type);
    public static bool IsStackable(string type) => Stackable.Contains(type);
    public static bool IsSupply(string type) => type is AlchemySupplies or CookingSupplies;
    public static bool IsGear(string type) => type is "weapon" or "armor";
    public static bool IsRecipe(string type) => Alchemy.Contains(type) || Cooking.Contains(type) || IsGear(type);
    public static bool IsCooking(string type) => Cooking.Contains(type);
    public static string CraftPermission(string type) => IsGear(type) ? BlacksmithPermission : IsCooking(type) ? "items.cooking.create" : "items.potion.create";
    public static string RecipePermission(string type) => IsGear(type) ? BlacksmithRecipePermission : IsCooking(type) ? "items.cooking.define" : "items.potion.define";
    public static string QuotaKind(string type) => IsGear(type) ? AmorRP.Core.Combat.CombatRules.BlacksmithingQuota : IsCooking(type) ? "cooking_points" : "potion_points";
}
