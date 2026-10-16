using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record GroupEdit([property: JsonRequired] string Name, [property: JsonRequired] string Description)
{
    public const int MaximumDescriptionCharacters = 300;
}
