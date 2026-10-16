using System.ComponentModel.DataAnnotations;
namespace AmorRP.Server.Options;

public sealed class SessionPolicyOptions
{
    [Range(5, 60)] public int AccessMinutes { get; set; } = 30;
    [Range(1, 90)] public int RefreshDays { get; set; } = 30;
    [Range(2, 20)] public int LoginMinutes { get; set; } = 10;
    [Range(1, 100)] public int MaxSessionsPerCharacter { get; set; } = 20;
    [Range(1, 1024)] public int MaxPendingLogins { get; set; } = 256;
    [Range(1, 64)] public int MaxPendingLoginsPerNetwork { get; set; } = 5;
    [Range(1, 120)] public int LoginStartsPerMinutePerNetwork { get; set; } = 10;
    [Range(1, 10)] public int MaxPairingFailures { get; set; } = 5;
}
