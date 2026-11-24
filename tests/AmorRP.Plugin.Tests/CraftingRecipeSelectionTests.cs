using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Plugin.Features.Crafting;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class CraftingRecipeSelectionTests
{
    private static Definition Recipe(string name, string type = "potion") => new(Guid.NewGuid(), Guid.NewGuid(), type,
        1, 1, name, Guid.NewGuid(), "Description", true, false, new(5, "Message"));

    [Fact]
    public void Search_uses_all_enabled_recipes_for_the_correct_discipline_and_hides_retired_definitions()
    {
        var potion = Recipe("Amber tea tincture"); var meal = Recipe("Tea biscuits", "meal");
        var disabled = Recipe("Tea medicine", "medicine") with { Enabled = false };
        var retired = Recipe("Tea poison", "poison") with { Retired = true };
        var book = Recipe("Tea diary", "book");
        var recipes = new[] { potion, meal, disabled, retired, book };
        Assert.Equal(potion.Id, Assert.Single(CraftingRecipeSelection.View(recipes, CraftingDiscipline.Alchemy, " TEA ")).Id);
        Assert.Equal(meal.Id, Assert.Single(CraftingRecipeSelection.View(recipes, CraftingDiscipline.Cooking, "tea")).Id);
    }

    [Fact]
    public void Refreshes_and_reordering_keep_the_selected_recipe_identity_and_use_its_latest_revision_and_cost()
    {
        var a = Recipe("Amber"); var b = Recipe("Basil");
        var selected = new CraftingRecipeSelection(); selected.Select(b.Id);
        var changed = b with { Name = "Aardvark", Revision = 2, Potion = new(9, "New message") };
        var view = CraftingRecipeSelection.View([a, changed], CraftingDiscipline.Alchemy, "");
        var chosen = view[selected.Resolve(view)];
        Assert.Equal(b.Id, chosen.Id); Assert.Equal(2, chosen.Revision); Assert.Equal(9, chosen.Potion.CreationCost);
    }

    [Fact]
    public void A_removed_or_filtered_selection_chooses_a_valid_remaining_recipe_and_an_empty_view_cannot_reuse_it()
    {
        var a = Recipe("Amber"); var b = Recipe("Basil");
        var selected = new CraftingRecipeSelection(); selected.Select(b.Id);
        var filtered = CraftingRecipeSelection.View([a, b], CraftingDiscipline.Alchemy, "Amber");
        Assert.Equal(a.Id, filtered[selected.Resolve(filtered)].Id);
        selected.Resolve([]); Assert.Null(selected.SelectedId);
        selected.Select(b.Id); selected.Reset(); Assert.Null(selected.SelectedId);
    }
}
