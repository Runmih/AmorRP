using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;

namespace AmorRP.Plugin.Features.Crafting;

internal sealed class CraftingRecipeSelection
{
    public Guid? SelectedId { get; private set; }
    public static Definition[] View(IEnumerable<Definition> definitions, bool cooking, string search)
    {
        var types = cooking ? CraftingTypes.Cooking : CraftingTypes.Alchemy;
        return definitions.Where(x => !x.Retired && x.Enabled && types.Contains(x.TypeId)
                && x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray();
    }
    public int Resolve(Definition[] recipes)
    {
        var index = Math.Max(0, Array.FindIndex(recipes, x => x.Id == SelectedId));
        SelectedId = recipes.Length == 0 ? null : recipes[index].Id;
        return index;
    }
    public void Select(Guid id) => SelectedId = id;
    public void Reset() => SelectedId = null;
}
