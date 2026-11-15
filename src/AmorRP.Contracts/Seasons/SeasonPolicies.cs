namespace AmorRP.Contracts.Seasons;

public sealed record SeasonPolicies([property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, int Version, DateTimeOffset CurrentPeriodStart, DateTimeOffset NextResetAt, PolicyValues Current, PolicyValues Next);
