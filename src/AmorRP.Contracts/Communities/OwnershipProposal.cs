using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record OwnershipProposal([property: JsonRequired] Guid RecipientCharacterId);
