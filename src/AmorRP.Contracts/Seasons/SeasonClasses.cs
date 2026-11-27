using AmorRP.Contracts.Combat;

namespace AmorRP.Contracts.Seasons;

public sealed record SeasonClass(Guid Id, string Name, string Description, string[] Grants,
    Guid[]? SkillIds = null, ClassCraftingBonuses? Crafting = null, string DownedBehavior = "normal");

public sealed record ClassCraftingBonuses(int Alchemy = 0, int Cooking = 0, int Gathering = 0, int Blacksmithing = 0);
public sealed record ClassPolicy(int Slots, long ResetPrice, SeasonClass[] Classes, int GroupRecipeLimit = 10);
public sealed record ClassSelection(ClassPolicy Policy, int PolicyVersion, Guid[] Selected, int SelectionVersion, bool FreeCorrection, CombatSkill[]? Abilities = null);
public sealed record ClassPolicyEdit(int ExpectedVersion, ClassPolicy Policy);
public sealed record ClassSelectionEdit(int PolicyVersion, int SelectionVersion, Guid[] Selected, long ConfirmedPrice);
