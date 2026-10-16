using System.Net;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private readonly TradeInbox tradeInbox = new();
    private Task<Action>? inboxWork;
    private Group[]? inboxGroups;
    private Guid? inboxCharacter;
    private DateTimeOffset nextInboxPoll;
    private long inboxScope, inboxData;
    private string inboxError = "";

    public int PendingTradeRequests => session == null ? 0 : tradeInbox.Pending(DateTimeOffset.UtcNow).Length;
    public string TradeNotificationStatus => inboxError.Length > 0 ? inboxError : tradePollError;

    public void OpenTradeFromLauncher()
    {
        var request = tradeInbox.Pending(DateTimeOffset.UtcNow)
            .OrderBy(t => t.GroupId == group?.Id ? 0 : 1).FirstOrDefault();
        if (request != null)
        {
            if (request.GroupId != group?.Id)
            {
                if (TradeBlocksGroupSwitch || WriteBlocked || work != null)
                {
                    tradeFeedback = "Finish or recover the current action before opening another group's request.";
                    OpenTradeWindow?.Invoke(); return;
                }
                var target = groups.FirstOrDefault(g => g.Id == request.GroupId);
                if (target == null) return;
                SwitchGroup(target);
            }
            ObserveTrade(request); tradeSelection = request.Id;
            contextTarget = null; tradeTargetId = null; requestedTradeTab = 0;
        }
        tradeWindowCloseRequested = false;
        OpenTradeWindow?.Invoke();
    }

    private void TickTradeInbox()
    {
        if (inboxCharacter != session?.Character.Id || !ReferenceEquals(inboxGroups, groups))
        {
            inboxCharacter = session?.Character.Id; inboxGroups = groups;
            var changed = tradeInbox.Bind(inboxCharacter, groups.Select(g => g.Id));
            foreach (var trade in trades) tradeInbox.Observe(trade.GroupId, trade);
            if (changed) nextInboxPoll = default;
        }
        if (inboxWork is { IsCompleted: true })
        {
            var done = inboxWork; inboxWork = null;
            if (done.IsCompletedSuccessfully && inboxData == dataGeneration) done.Result();
            else
            {
                _ = done.Exception;
                if (done.IsCanceled || inboxData != dataGeneration) nextInboxPoll = default;
                else { inboxError = "Trade notifications could not refresh. Retrying shortly."; nextInboxPoll = DateTimeOffset.UtcNow.AddSeconds(30); }
            }
        }
        if (inboxWork != null || session == null || origin == null || !serviceCompatible || currentCommand != null
            || session.AccessExpiresAt <= DateTimeOffset.UtcNow || DateTimeOffset.UtcNow < nextInboxPoll) return;
        // The selected group already has its normal trade poll. Read only the
        // other joined groups, once per 15 seconds, without touching UI drafts.
        var ids = groups.Where(g => g.Id != group?.Id).Select(g => g.Id).ToArray();
        nextInboxPoll = DateTimeOffset.UtcNow.AddSeconds(15);
        if (ids.Length == 0) { inboxError = ""; return; }
        var uri = origin; var token = session.AccessToken; var ct = lifetime.Token;
        var epoch = inboxScope; var actor = session.Character.Id;
        inboxData = dataGeneration;
        inboxWork = Task.Run(async () =>
        {
            var results = new List<(Guid Group, Trade? Trade)>();
            var refreshMembership = false;
            foreach (var id in ids)
            {
                try
                {
                    var page = await client.GetAsync<TradePage>(uri, $"api/v1/groups/{id}/trades?limit=1", token, ct);
                    var latest = page.Items.FirstOrDefault();
                    if (latest != null && latest.GroupId != id) throw new InvalidOperationException("Trade response belongs to another group.");
                    results.Add((id, latest));
                }
                catch (FoundationApiException error) when (error.Status is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                { results.Add((id, null)); refreshMembership = true; }
            }
            return (Action)(() =>
            {
                if (epoch != inboxScope || actor != session?.Character.Id || uri != origin) return;
                foreach (var result in results)
                    if (result.Group != group?.Id) tradeInbox.Observe(result.Group, result.Trade);
                if (refreshMembership) refreshNeeded = true;
                inboxError = "";
            });
        }, ct);
    }

    private void ClearTradeInbox()
    {
        inboxScope++; tradeInbox.Clear(); inboxGroups = null; inboxCharacter = null; inboxError = ""; nextInboxPoll = default;
        if (inboxWork != null) _ = inboxWork.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
        inboxWork = null;
    }
}
