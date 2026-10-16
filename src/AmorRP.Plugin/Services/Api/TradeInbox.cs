using AmorRP.Contracts.Trading;

namespace AmorRP.Plugin.Services.Api;

// One newest trade per joined group. Identity and membership changes remove old
// scope data before any request can become a launcher badge or an opening target.
public sealed class TradeInbox
{
    private Guid? character;
    private HashSet<Guid> groups = [];
    private readonly Dictionary<Guid, Trade> latest = [];

    public bool Bind(Guid? characterId, IEnumerable<Guid> groupIds)
    {
        var joined = groupIds.ToHashSet();
        if (character == characterId && groups.SetEquals(joined)) return false;
        if (character != characterId) latest.Clear();
        character = characterId; groups = joined;
        foreach (var id in latest.Keys.Where(id => !groups.Contains(id)).ToArray()) latest.Remove(id);
        return true;
    }

    public void Observe(Guid groupId, Trade? trade)
    {
        if (character == null || !groups.Contains(groupId)) return;
        if (trade == null) { latest.Remove(groupId); return; }
        if (trade.GroupId != groupId) throw new InvalidOperationException("Trade response belongs to another group.");
        if (latest.TryGetValue(groupId, out var previous) && previous.Id == trade.Id)
        {
            if (previous.Revision > trade.Revision) return;
            if (previous.State is "cancelled" or "completed" or "expired" && trade.State is "invited" or "open" or "confirming") return;
        }
        latest[groupId] = trade;
    }

    public Trade[] Pending(DateTimeOffset now) => latest.Values
        .Where(t => t.State == "invited" && t.Recipient.Character.Id == character && t.ExpiresAt > now)
        .OrderBy(t => t.ExpiresAt).ThenBy(t => t.Id).ToArray();

    public void Clear() { character = null; groups.Clear(); latest.Clear(); }
}
