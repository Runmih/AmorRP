namespace AmorRP.Server.Options;

public sealed class SeasonPolicyOptions
{
    public string[] AdministratorLodestoneIds { get; set; } = [];
    public bool IsValid() => AdministratorLodestoneIds.All(x => x.Length is > 0 and <= 20 && x.All(char.IsAsciiDigit));
}
