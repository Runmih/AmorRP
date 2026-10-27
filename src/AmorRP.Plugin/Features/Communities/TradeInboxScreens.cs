using System.Net;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private readonly TradeInbox tradeInbox = new();
    private Task<Action>? inboxWork;
    private Community[]? inboxCommunities;
    private Guid? inboxCharacter;
    private DateTimeOffset nextInboxPoll;
    private long inboxScope, inboxData;
    private string inboxError = "";

    public int PendingTradeRequests => session == null ? 0 : tradeInbox.Pending(DateTimeOffset.UtcNow).Length;
    public string TradeNotificationStatus => inboxError.Length > 0 ? inboxError : tradePollError;

    public void OpenTradeFromLauncher()
    {
        var request = tradeInbox.Pending(DateTimeOffset.UtcNow)
            .OrderBy(t => t.CommunityId == community?.Id ? 0 : 1).FirstOrDefault();
        if (request != null)
        {
            if (request.CommunityId != community?.Id)
            {
                if (TradeBlocksCommunitySwitch || WriteBlocked || Busy)
                {
                    tradeFeedback = "Finish or recover the current action before opening another community's request.";
                    OpenTradeWindow?.Invoke(); return;
                }
                var target = communities.FirstOrDefault(g => g.Id == request.CommunityId);
                if (target == null) return;
                SwitchCommunity(target);
            }
            ObserveTrade(request); tradeSelection = request.Id;
            contextTarget = null; tradeTargetId = null; requestedTradeTab = 0;
        }
        tradeWindowCloseRequested = false;
        OpenTradeWindow?.Invoke();
    }

    private void TickTradeInbox()
    {
        if (inboxCharacter != session?.Character.Id || !ReferenceEquals(inboxCommunities, communities))
        {
            inboxCharacter = session?.Character.Id; inboxCommunities = communities;
            var changed = tradeInbox.Bind(inboxCharacter, communities.Select(g => g.Id));
            foreach (var trade in trades) tradeInbox.Observe(trade.CommunityId, trade);
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
        // The selected community already has its normal trade poll. Read only the
        // other joined communities, once per 15 seconds, without touching UI drafts.
        var ids = communities.Where(g => g.Id != community?.Id).Select(g => g.Id).ToArray();
        nextInboxPoll = DateTimeOffset.UtcNow.AddSeconds(15);
        if (ids.Length == 0) { inboxError = ""; return; }
        var uri = origin; var token = session.AccessToken; var ct = requests.Token;
        var epoch = inboxScope; var actor = session.Character.Id;
        inboxData = dataGeneration;
        inboxWork = Task.Run(async () =>
        {
            var results = new List<(Guid Community, Trade? Trade)>();
            var refreshMembership = false;
            foreach (var id in ids)
            {
                try
                {
                    var page = await client.GetAsync<TradePage>(uri, $"api/v2/communities/{id}/trades?limit=1", token, ct);
                    var latest = page.Items.FirstOrDefault();
                    if (latest != null && latest.CommunityId != id) throw new InvalidOperationException("Trade response belongs to another community.");
                    results.Add((id, latest));
                }
                catch (FoundationApiException error) when (error.Status is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                { results.Add((id, null)); refreshMembership = true; }
            }
            return (Action)(() =>
            {
                if (epoch != inboxScope || actor != session?.Character.Id || uri != origin) return;
                foreach (var result in results)
                    if (result.Community != community?.Id) tradeInbox.Observe(result.Community, result.Trade);
                if (refreshMembership) refreshNeeded = true;
                inboxError = "";
            });
        }, ct);
    }

    private void ClearTradeInbox()
    {
        inboxScope++; tradeInbox.Clear(); inboxCommunities = null; inboxCharacter = null; inboxError = ""; nextInboxPoll = default;
        if (inboxWork != null) _ = inboxWork.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
        inboxWork = null;
    }
}
