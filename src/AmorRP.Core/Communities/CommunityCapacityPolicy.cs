namespace AmorRP.Core.Communities;

/// <summary>Community capacity configuration. Membership admission is implemented in M2.</summary>
public sealed record CommunityCapacityPolicy
{
    public CommunityCapacityPolicy(int ownedCommunities, int joinedCommunities)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownedCommunities);
        ArgumentOutOfRangeException.ThrowIfLessThan(joinedCommunities, ownedCommunities);
        OwnedCommunities = ownedCommunities;
        JoinedCommunities = joinedCommunities;
    }

    public int OwnedCommunities { get; }
    public int JoinedCommunities { get; }
}
