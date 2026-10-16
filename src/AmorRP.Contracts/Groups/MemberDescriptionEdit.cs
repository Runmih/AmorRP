namespace AmorRP.Contracts.Groups;

public sealed record MemberDescriptionEdit(string Description)
{
    public const int MaximumCharacters = 300;
}
