namespace AmorRP.Contracts.Inventory;

public sealed record Quota(string Kind, DateTimeOffset PeriodStart, DateTimeOffset ResetAt, int Limit, int Used, int Remaining);
