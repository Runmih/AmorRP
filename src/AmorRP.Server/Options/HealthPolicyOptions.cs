using System.ComponentModel.DataAnnotations;

namespace AmorRP.Server.Options;

public sealed class HealthPolicyOptions
{
    [Range(1, 60)] public int ReadinessRefreshSeconds { get; set; } = 5;
}
