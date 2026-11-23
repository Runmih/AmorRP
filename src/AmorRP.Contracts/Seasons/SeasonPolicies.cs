using System.Text.Json.Serialization;
using AmorRP.Contracts.Inventory;

namespace AmorRP.Contracts.Seasons;

public sealed record SeasonPolicies([property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, int Version, DateTimeOffset CurrentPeriodStart, DateTimeOffset NextResetAt, PolicyValues Current, PolicyValues Next,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GatheringSettings? Gathering = null);
