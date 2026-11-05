namespace AmorRP.Contracts.Currency;

public sealed record BillingPolicy(bool AllowDebt, int Version, string Etag);
public sealed record BillingPolicyEdit(bool AllowDebt);
public sealed record BillCreate(Guid BillId, Guid CharacterId, string Amount, string Service);
public sealed record Bill(Guid Id, Guid CommunityId, Guid GroupId, string GroupName, Guid IssuerCharacterId, string IssuerName,
    Guid CharacterId, string CharacterName, string Service, string Amount, string Collected, string Outstanding, DateTimeOffset CreatedAt);
public sealed record BillPage(Bill[] Items, string? NextCursor, string TotalOutstanding = "0");
public sealed record BillPayment(Guid Id, Guid BillId, string Amount, string CharacterBefore, string CharacterAfter,
    string TreasuryBefore, string TreasuryAfter, string Source, Guid? SourceId, DateTimeOffset OccurredAt);
public sealed record BillPaymentPage(BillPayment[] Items, string? NextCursor);
