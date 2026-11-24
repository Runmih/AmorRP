namespace AmorRP.Contracts.Groups;

public static class GroupCapabilities
{
    public const string Announcements = "announcements.manage";
    public const string Notice = "notice.manage";
    public const string Events = "events.manage";
    public const string Discord = "discord.bot";
    public const string Storage = "items.storage";
    public const string StorageWithdraw = "items.storage.withdraw";
    public const string Treasury = "treasury";
    public const string TreasuryRecords = "treasury.records";
    public const string Billing = "treasury.bill";
    public const string TreasuryWithdraw = "treasury.withdraw";
    public static IReadOnlyList<string> DefaultFeatures { get; } = Array.AsReadOnly(new[] { Announcements, Notice, Events });
    public static IReadOnlyList<string> Features { get; } = Array.AsReadOnly(new[] { Announcements, Notice, Events, Treasury, Storage, Discord });
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Announcements, Notice, Events, TreasuryRecords, TreasuryWithdraw, Billing, Storage, StorageWithdraw });
    public static string FeatureFor(string capability) => capability is TreasuryRecords or TreasuryWithdraw or Billing ? Treasury : capability == StorageWithdraw ? Storage : capability;
    public static bool Enabled(IEnumerable<string> features, string capability) => features.Contains(FeatureFor(capability));
}

public sealed record GroupTrustEdit(bool Trusted);
public sealed record GroupFeaturesEdit(string[] EnabledFeatures);
public sealed record GroupStaffEdit(string[] Capabilities,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int? CommissionPercent = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool UpdateCommission = false);
