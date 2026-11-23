namespace AmorRP.Contracts.Seasons;

public sealed record SeasonClass(Guid Id, string Name, string Description, string[] Grants);
public sealed record ClassPolicy(int Slots, long ResetPrice, SeasonClass[] Classes, int GroupRecipeLimit = 10);
public sealed record ClassSelection(ClassPolicy Policy, int PolicyVersion, Guid[] Selected, int SelectionVersion, bool FreeCorrection);
public sealed record ClassPolicyEdit(int ExpectedVersion, ClassPolicy Policy);
public sealed record ClassSelectionEdit(int PolicyVersion, int SelectionVersion, Guid[] Selected, long ConfirmedPrice);
