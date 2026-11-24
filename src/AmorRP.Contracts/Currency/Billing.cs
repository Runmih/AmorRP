namespace AmorRP.Contracts.Currency;

public sealed record BillingPolicy(bool AllowDebt, int Version, string Etag, int TaxPercent = 0, string? SeasonTreasury = null);
public sealed record BillingPolicyEdit(bool AllowDebt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    int? TaxPercent = null);
public sealed record GroupBillingSettings(int CommissionPercent, bool RequireClockIn, DateTimeOffset? ClockedInAt, string Etag, int? MyCommissionPercent = null);
public sealed record GroupBillingEdit(int CommissionPercent, bool RequireClockIn);
public sealed record ClockInEdit(bool ClockedIn, Guid? CharacterId = null);
public sealed record BillAllocation(string SeasonTax, string Commission, string GroupTreasury);
public sealed record BillCreate(Guid BillId, Guid CharacterId, string Amount, string Service);
public sealed record Bill(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, Guid GroupId, string GroupName, Guid IssuerCharacterId, string IssuerName,
    Guid CharacterId, string CharacterName, string Service, string Amount, string Collected, string Outstanding, DateTimeOffset CreatedAt,
    int TaxPercent = 0, int CommissionPercent = 0, BillAllocation? Allocation = null);
public sealed record BillPage(Bill[] Items, string? NextCursor, string TotalOutstanding = "0");
public sealed record BillPayment(Guid Id, Guid BillId, string Amount, string CharacterBefore, string CharacterAfter,
    string TreasuryBefore, string TreasuryAfter, string Source, Guid? SourceId, DateTimeOffset OccurredAt, BillAllocation? Allocation = null, Guid? FundingGroupId = null);
public sealed record BillPaymentPage(BillPayment[] Items, string? NextCursor);
