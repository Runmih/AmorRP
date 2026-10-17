namespace AmorRP.Contracts.Communities;

public sealed record MemberDescriptionEdit(string Description)
{
    public const int MaximumCharacters = 300;
}
