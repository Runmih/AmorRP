namespace AmorRP.Contracts.Inventory;

public sealed record LetterCreate(Guid CategoryId, string Title, string Body, string TypeId = "letter");
