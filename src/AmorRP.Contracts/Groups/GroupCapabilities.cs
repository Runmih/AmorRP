namespace AmorRP.Contracts.Groups;

public static class GroupCapabilities
{
    public const string Announcements = "announcements.manage";
    public const string Notice = "notice.manage";
    public const string Events = "events.manage";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Announcements, Notice, Events });
}

public sealed record GroupFeaturesEdit(string[] EnabledFeatures);
public sealed record GroupStaffEdit(string[] Capabilities);
