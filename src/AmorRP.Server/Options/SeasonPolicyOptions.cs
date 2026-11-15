namespace AmorRP.Server.Options;

public sealed class SeasonPolicyOptions
{
    // Explicit rollout switch: upgrading a database never silently chooses or resets a season.
    public bool Enabled { get; set; }
    public string[] AdministratorLodestoneIds { get; set; } = [];
    public bool IsValid() => AdministratorLodestoneIds.All(x => x.Length is > 0 and <= 20 && x.All(char.IsAsciiDigit));
}
