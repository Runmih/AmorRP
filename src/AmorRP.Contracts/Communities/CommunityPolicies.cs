namespace AmorRP.Contracts.Communities;

public sealed record CommunityPolicies(Guid CommunityId, int Version, DateTimeOffset CurrentPeriodStart, DateTimeOffset NextResetAt, PolicyValues Current, PolicyValues Next);
