namespace AmorRP.Core.Groups;

/// <summary>Group capacity configuration. Membership admission is implemented in M2.</summary>
public sealed record GroupCapacityPolicy
{
    public GroupCapacityPolicy(int ownedGroups, int joinedGroups)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownedGroups);
        ArgumentOutOfRangeException.ThrowIfLessThan(joinedGroups, ownedGroups);
        OwnedGroups = ownedGroups;
        JoinedGroups = joinedGroups;
    }

    public int OwnedGroups { get; }
    public int JoinedGroups { get; }
}
