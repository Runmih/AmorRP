namespace AmorRP.Contracts.Currency;

// Whole-unit quantities are strings, matching existing character balances.
public sealed record TreasuryTransfer(string Amount, string Note = "");
public sealed record TreasuryBalance([property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, Guid GroupId, Guid CurrencyId, string Owned, string Reserved, string Available, int Version);
public sealed record TreasuryReceipt(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, Guid GroupId, Guid OperationId, Guid ActorCharacterId, Guid CharacterId,
    string CharacterName, string Kind, string Amount, string GroupBefore, string GroupAfter, string CharacterBefore, string CharacterAfter, string Note, DateTimeOffset OccurredAt);
public sealed record TreasuryReceiptPage(TreasuryReceipt[] Items, string? NextCursor);
public sealed record TreasuryTransferResult(TreasuryBalance Treasury, Balance CharacterBalance, TreasuryReceipt Receipt);
public sealed record TreasuryReturn([property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, Guid GroupId, [property: System.Text.Json.Serialization.JsonPropertyName("seasonName")] string CommunityName, string GroupName, string CurrencyName, string Amount);
public sealed record TreasuryReturnPage(TreasuryReturn[] Items, string? NextCursor);
