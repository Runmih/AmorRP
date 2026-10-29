namespace AmorRP.Contracts.Groups;

public static class GroupCapabilities
{
    public const string Announcements = "announcements.manage";
    public const string Notice = "notice.manage";
    public const string Events = "events.manage";
    public const string Treasury = "treasury";
    public const string TreasuryRecords = "treasury.records";
    public const string TreasuryWithdraw = "treasury.withdraw";
    public static IReadOnlyList<string> DefaultFeatures { get; } = Array.AsReadOnly(new[] { Announcements, Notice, Events });
    public static IReadOnlyList<string> Features { get; } = Array.AsReadOnly(new[] { Announcements, Notice, Events, Treasury });
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Announcements, Notice, Events, TreasuryRecords, TreasuryWithdraw });
    public static string FeatureFor(string capability) => capability is TreasuryRecords or TreasuryWithdraw ? Treasury : capability;
    public static bool Enabled(IEnumerable<string> features, string capability) => features.Contains(FeatureFor(capability));
}

public sealed record GroupFeaturesEdit(string[] EnabledFeatures);
public sealed record GroupStaffEdit(string[] Capabilities);
