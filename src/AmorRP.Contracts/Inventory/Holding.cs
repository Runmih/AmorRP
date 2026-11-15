namespace AmorRP.Contracts.Inventory;

public sealed record Holding(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, Guid CharacterId, string TypeId, string Name, Guid CategoryId, int Quantity, int Reserved, int Available, int Version, Guid? DefinitionId, int? DefinitionRevision, Guid? LetterId);
