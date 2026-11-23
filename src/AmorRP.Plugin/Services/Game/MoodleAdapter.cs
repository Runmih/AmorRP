using AmorRP.Contracts.Inventory;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

// Moodles IPC v4 status tuple, version 2. Dalamud converts enum fields by value
// when the receiving plugin owns the enum types.
using MoodleStatus = (int Version, System.Guid Id, int Icon, string Title, string Description, string Vfx,
    long DurationMilliseconds, int Type, int Stacks, int StackSteps, uint Modifiers, System.Guid ChainedStatus,
    int ChainTrigger, string Applier, string Dispeller, bool Permanent);

namespace AmorRP.Plugin.Services.Game;

public sealed class MoodleAdapter(IDalamudPluginInterface plugin, IObjectTable objects)
{
    private const string VersionGate = "Moodles.Version";
    private const string StatusGate = "Moodles.GetStatusManagerInfoByPlayerV2";
    private const string ApplyGate = "Moodles.AddOrUpdateMoodleByDataByPlayerV2";
    private const string RemoveGate = "Moodles.RemoveMoodlesByPlayerV2";
    private readonly Dictionary<Guid, (MoodleGrant Grant, DateTimeOffset RefreshAt, string Outcome)> attempts = [];
    private string? characterScope, lastAvailability, lastReadError, lastRemoval;
    private DateTimeOffset nextProbe;
    private bool ready;

    public bool IsReady(string scope, DateTimeOffset now, Action<string, string> report)
    {
        if (characterScope != scope)
        {
            attempts.Clear();
            characterScope = scope;
            lastAvailability = lastReadError = lastRemoval = null;
            nextProbe = default;
        }
        if (now < nextProbe) return ready;
        nextProbe = now.AddSeconds(5);
        ready = false;
        try
        {
            if (objects.LocalPlayer == null)
            {
                ReportChange(ref lastAvailability, "Moodles check skipped. The local character is unavailable.", "", report);
                return false;
            }
            var version = plugin.GetIpcSubscriber<int>(VersionGate).InvokeFunc();
            ready = version == 4;
            ReportChange(ref lastAvailability, ready ? "Moodles IPC is ready." : "Moodles IPC version is unsupported.",
                $"{VersionGate} returned {version}. Amor RP expects 4.", report);
        }
        catch (IpcNotReadyError error)
        {
            ReportChange(ref lastAvailability, "Moodles IPC is unavailable.", $"{VersionGate}: {ErrorDetails(error)}", report);
        }
        catch (Exception error)
        {
            ReportChange(ref lastAvailability, "Moodles version check failed.", $"{VersionGate}: {ErrorDetails(error)}", report);
        }
        return ready;
    }

    public void Reconcile(string playerName, string world, MoodleGrants grants, DateTimeOffset now, Action<string, string> report)
    {
        IPlayerCharacter player;
        List<MoodleStatus> statuses;
        try
        {
            if (objects.LocalPlayer is not IPlayerCharacter local || local.Name.TextValue != playerName)
            {
                ReportChange(ref lastReadError, "Moodles attempt skipped. The local character does not match the Amor RP session.", "", report);
                return;
            }
            player = local;
            statuses = ReadStatuses(player);
            lastReadError = null;
        }
        catch (Exception error)
        {
            ReportChange(ref lastReadError, "Moodles status read failed.", $"{StatusGate}: {ErrorDetails(error)}", report);
            return;
        }

        var active = grants.Active.Where(g => g.ExpiresAt == null || g.ExpiresAt > grants.ServerTime).Select(g => g.Id).ToHashSet();
        var removed = grants.Remove.Concat(attempts.Keys.Where(id => !active.Contains(id))).Distinct()
            .Where(id => statuses.Any(status => status.Id == id)).ToList();
        if (removed.Count > 0) TryRemove(player, removed, report);
        else lastRemoval = null;
        foreach (var id in attempts.Keys.Where(id => !active.Contains(id)).ToArray()) attempts.Remove(id);

        foreach (var grant in grants.Active)
        {
            var effect = grant.Effect;
            var remaining = grant.ExpiresAt is { } expiry ? expiry - grants.ServerTime : TimeSpan.FromSeconds(Math.Min(90, effect.DurationSeconds));
            if (remaining < TimeSpan.FromSeconds(1)) continue;
            var existed = statuses.Any(status => status.Id == grant.Id);
            var previous = attempts.GetValueOrDefault(grant.Id);
            if (previous.Grant == grant && (now < previous.RefreshAt || (grant.ExpiresAt != null && existed))) continue;

            MoodleStatus data = (2, grant.Id, effect.IconId, effect.Name, effect.Description, "",
                (long)remaining.TotalSeconds * 1000, effect.Debuff ? 1 : 0, 1, 0, 0, Guid.Empty, 0, $"{playerName}@{world}", "", false);
            var (outcome, details) = TryApply(player, data, existed);
            if (previous.Grant != grant || previous.Outcome != outcome)
                report($"{effect.Name} · {outcome}", details);
            var retry = grant.ExpiresAt == null ? remaining.TotalSeconds / 2 : 5;
            attempts[grant.Id] = (grant, now.AddSeconds(retry), outcome);
        }
    }

    private List<MoodleStatus> ReadStatuses(IPlayerCharacter player) =>
        plugin.GetIpcSubscriber<IPlayerCharacter, List<MoodleStatus>>(StatusGate).InvokeFunc(player)
        ?? throw new InvalidOperationException("Moodles returned a null status list.");

    private (string Outcome, string Details) TryApply(IPlayerCharacter player, MoodleStatus data, bool existed)
    {
        try
        {
            plugin.GetIpcSubscriber<MoodleStatus, IPlayerCharacter, object>(ApplyGate).InvokeAction(data, player);
        }
        catch (Exception error)
        {
            return ("Moodles apply failed", $"{ApplyGate}: {ErrorDetails(error)}");
        }

        var submitted = $"{ApplyGate} completed without a return value.";
        try
        {
            var match = ReadStatuses(player).FirstOrDefault(status => status.Id == data.Id);
            if (match.Id == Guid.Empty)
                return ("Moodles did not list the effect", $"{submitted}\n{StatusGate} returned no matching effect. This IPC provides no rejection reason. Check /xllog for Moodles [IPC] messages.");
            var outcome = existed ? "Moodles still lists the effect after the refresh request" : "Moodles lists the effect after application";
            return (outcome, $"{submitted}\n{StatusGate} returned the matching effect with icon {match.Icon}, stacks {match.Stacks} and duration {match.DurationMilliseconds} ms.");
        }
        catch (Exception error)
        {
            return ("Moodles effect could not be verified", $"{submitted}\n{StatusGate}: {ErrorDetails(error)}");
        }
    }

    private void TryRemove(IPlayerCharacter player, List<Guid> ids, Action<string, string> report)
    {
        try
        {
            plugin.GetIpcSubscriber<List<Guid>, IPlayerCharacter, object>(RemoveGate).InvokeAction(ids, player);
        }
        catch (Exception error)
        {
            ReportChange(ref lastRemoval, "Moodles removal failed.", $"{RemoveGate}: {ErrorDetails(error)}", report);
            return;
        }
        var submitted = $"{RemoveGate} completed without a return value for {ids.Count} effect(s).";
        try
        {
            var stillPresent = ReadStatuses(player).Count(status => ids.Contains(status.Id));
            ReportChange(ref lastRemoval, stillPresent == 0 ? "Moodles no longer lists the cleared effects." : "Moodles still lists effects after removal.",
                $"{submitted}\n{StatusGate} returned {stillPresent} matching effect(s).", report);
        }
        catch (Exception error)
        {
            ReportChange(ref lastRemoval, "Moodles removal could not be verified.", $"{submitted}\n{StatusGate}: {ErrorDetails(error)}", report);
        }
    }

    private static void ReportChange(ref string? last, string summary, string details, Action<string, string> report)
    {
        var entry = summary + "\n" + details;
        if (last == entry) return;
        last = entry;
        report(summary, details);
    }

    private static string ErrorDetails(Exception error)
    {
        var cause = error.GetBaseException();
        return ReferenceEquals(cause, error)
            ? $"{error.GetType().Name}: {error.Message}"
            : $"{error.GetType().Name}: {error.Message}\n{cause.GetType().Name}: {cause.Message}";
    }
}
