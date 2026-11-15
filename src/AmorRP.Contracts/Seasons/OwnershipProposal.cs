using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Seasons;

public sealed record OwnershipProposal([property: JsonRequired] Guid RecipientCharacterId);
