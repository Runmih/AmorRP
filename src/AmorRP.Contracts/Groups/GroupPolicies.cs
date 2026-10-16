namespace AmorRP.Contracts.Groups;

public sealed record GroupPolicies(Guid GroupId, int Version, DateTimeOffset CurrentPeriodStart, DateTimeOffset NextResetAt, PolicyValues Current, PolicyValues Next);
