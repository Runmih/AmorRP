using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record OwnershipProposal([property: JsonRequired] Guid RecipientCharacterId);
