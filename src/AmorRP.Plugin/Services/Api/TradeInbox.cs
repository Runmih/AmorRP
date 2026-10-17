using AmorRP.Contracts.Trading;

namespace AmorRP.Plugin.Services.Api;

// One newest trade per joined community. Identity and membership changes remove old
// scope data before any request can become a launcher badge or an opening target.
public sealed class TradeInbox
{
    private Guid? character;
    private HashSet<Guid> communities = [];
    private readonly Dictionary<Guid, Trade> latest = [];

    public bool Bind(Guid? characterId, IEnumerable<Guid> communityIds)
    {
        var joined = communityIds.ToHashSet();
        if (character == characterId && communities.SetEquals(joined)) return false;
        if (character != characterId) latest.Clear();
        character = characterId; communities = joined;
        foreach (var id in latest.Keys.Where(id => !communities.Contains(id)).ToArray()) latest.Remove(id);
        return true;
    }

    public void Observe(Guid communityId, Trade? trade)
    {
        if (character == null || !communities.Contains(communityId)) return;
        if (trade == null) { latest.Remove(communityId); return; }
        if (trade.CommunityId != communityId) throw new InvalidOperationException("Trade response belongs to another community.");
        if (latest.TryGetValue(communityId, out var previous) && previous.Id == trade.Id)
        {
            if (previous.Revision > trade.Revision) return;
            if (previous.State is "cancelled" or "completed" or "expired" && trade.State is "invited" or "open" or "confirming") return;
        }
        latest[communityId] = trade;
    }

    public Trade[] Pending(DateTimeOffset now) => latest.Values
        .Where(t => t.State == "invited" && t.Recipient.Character.Id == character && t.ExpiresAt > now)
        .OrderBy(t => t.ExpiresAt).ThenBy(t => t.Id).ToArray();

    public void Clear() { character = null; communities.Clear(); latest.Clear(); }
}
