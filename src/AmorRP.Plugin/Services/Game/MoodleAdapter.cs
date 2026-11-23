using AmorRP.Contracts.Inventory;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// Moodles IPC v4 status tuple, version 2. Dalamud converts enum fields by value
// when the receiving plugin owns the enum types.
using MoodleStatus = (int Version, System.Guid Id, int Icon, string Title, string Description, string Vfx,
    long DurationMilliseconds, int Type, int Stacks, int StackSteps, uint Modifiers, System.Guid ChainedStatus,
    int ChainTrigger, string Applier, string Dispeller, bool Permanent);

namespace AmorRP.Plugin.Services.Game;

public sealed class MoodleAdapter(IDalamudPluginInterface plugin, IObjectTable objects)
{
    private readonly Dictionary<Guid, (MoodleGrant Grant, DateTimeOffset RefreshAt)> attempts = [];
    private string? characterScope;
    private DateTimeOffset nextProbe;
    private bool ready;

    public bool IsReady(DateTimeOffset now)
    {
        if (now < nextProbe) return ready;
        nextProbe = now.AddSeconds(5);
        try { ready = objects.LocalPlayer != null && plugin.GetIpcSubscriber<int>("Moodles.Version").InvokeFunc() == 4; }
        catch (Exception) { ready = false; }
        return ready;
    }

    public void Reconcile(string scope, string playerName, string world, MoodleGrants grants, DateTimeOffset now)
    {
        if (characterScope != scope)
        {
            attempts.Clear();
            characterScope = scope;
        }
        try
        {
            if (!IsReady(now) || objects.LocalPlayer is not IPlayerCharacter player || player.Name.TextValue != playerName) return;

            var statuses = plugin.GetIpcSubscriber<IPlayerCharacter, List<MoodleStatus>>("Moodles.GetStatusManagerInfoByPlayerV2")
                .InvokeFunc(player).Select(x => x.Id).ToHashSet();
            var active = grants.Active.Where(g => g.ExpiresAt == null || g.ExpiresAt > grants.ServerTime).Select(g => g.Id).ToHashSet();
            var removed = grants.Remove.Concat(attempts.Keys.Where(id => !active.Contains(id))).Distinct().Where(statuses.Contains).ToList();
            if (removed.Count > 0)
                plugin.GetIpcSubscriber<List<Guid>, IPlayerCharacter, object>("Moodles.RemoveMoodlesByPlayerV2").InvokeAction(removed, player);
            foreach (var id in attempts.Keys.Where(id => !active.Contains(id)).ToArray()) attempts.Remove(id);

            var apply = plugin.GetIpcSubscriber<MoodleStatus, IPlayerCharacter, object>("Moodles.AddOrUpdateMoodleByDataByPlayerV2");
            foreach (var grant in grants.Active)
            {
                var effect = grant.Effect;
                var remaining = grant.ExpiresAt is { } expiry ? expiry - grants.ServerTime : TimeSpan.FromSeconds(Math.Min(90, effect.DurationSeconds));
                if (remaining < TimeSpan.FromSeconds(1)) continue;
                var exists = statuses.Contains(grant.Id);
                if (attempts.TryGetValue(grant.Id, out var attempt) && attempt.Grant == grant)
                {
                    if (now < attempt.RefreshAt || (grant.ExpiresAt != null && exists)) continue;
                }
                var next = grant.ExpiresAt == null ? now.AddSeconds(remaining.TotalSeconds / 2) : now.AddSeconds(5);
                attempts[grant.Id] = (grant, next);
                MoodleStatus data = (2, grant.Id, effect.IconId, effect.Name, effect.Description, "",
                    (long)remaining.TotalSeconds * 1000, effect.Debuff ? 1 : 0, 1, 0, 0, Guid.Empty, 0, $"{playerName}@{world}", "", false);
                apply.InvokeAction(data, player);
            }
        }
        catch (Exception)
        {
            // Optional integration. No game action depends on IPC readiness or acceptance.
        }
    }
}
