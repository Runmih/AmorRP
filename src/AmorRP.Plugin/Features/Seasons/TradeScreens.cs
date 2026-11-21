using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private Trade[] trades = [];
    private Holding[] tradeInventory = [];
    private bool tradeInventoryLoaded;
    private string? tradeInventoryCursor;
    private Guid? tradeSelection, outgoingRecipient;
    private CharacterTarget? tradeRequestTarget;
    private bool resolvingTradeTarget;
    private string tradeRequestMessage = "";
    private Task<Action>? tradeWork;
    private long tradeView, tradeData;
    private DateTimeOffset nextTradePoll;
    private string tradePollError = "";
    private Season? switchAfterTrade;
    private TradeDraft? tradeDraft;
    private int submittedTradeGeneration, tradeConflictRetries;
    private bool tradeCancelRequested, tradeReadyRequested, tradeAcceptRequested, tradePreparing;
    private bool tradeWindowVisible, tradeWindowCloseRequested, confirmationStartedLocally;
    private Trade? finalReview;
    private Guid? popupForTrade;
    public Action? OpenTradeWindow { get; set; }
    public Action<bool>? SetTradeRequestWindow { get; set; }
    public Action? OpenSettingsWindow { get; set; }
    private static bool Active(Trade t) => t.State is "invited" or "open" or "confirming";
    private Trade? ActiveTrade => trades.FirstOrDefault(Active);
    // Incoming invitations reserve no assets and need not pin the active community.
    // A request we sent, or an accepted exchange, must be ended before switching.
    private bool TradeBlocksCommunitySwitch => ActiveTrade is { } t && (t.State != "invited" || t.Initiator.Character.Id == session?.Character.Id);
    private Trade? SelectedTrade => trades.FirstOrDefault(x => x.Id == tradeSelection) ?? ActiveTrade;
    private bool Reviewing(Trade t) => confirmationStartedLocally || t.Initiator.Confirmed || t.Recipient.Confirmed || t.State == "confirming";
    public void SelectTradeTarget(CharacterTarget target)
    {
        if (!BeginTradeRequest()) return;
        tradeRequestTarget = target;
    }

    private bool BeginTradeRequest()
    {
        if (ActiveTrade is { State: "open" or "confirming" })
        {
            OpenTradeFromLauncher();
            return false;
        }

        SetTradeRequestWindow?.Invoke(true);
        if (ActiveTrade != null || outgoingRecipient.HasValue || tradeRequestTarget != null || resolvingTradeTarget) return false;
        if (session == null || community == null || SeasonReadOnly || community.MyTradingRestricted)
        {
            tradeRequestMessage = session == null ? "Sign in from Settings to trade."
                : community == null || SeasonReadOnly ? "An active season is required to trade."
                : "Trading is restricted for your character.";
            return false;
        }
        if (WriteBlocked)
        {
            tradeRequestMessage = HasUnresolvedAction ? "Check the pending action in the Activity Log before sending a request."
                : "Amor RP is still connecting. Try again shortly.";
            return false;
        }

        tradeWindowCloseRequested = false;
        tradeRequestMessage = "";
        return true;
    }

    private void SendTradeRequest(Member target)
    {
        if (session == null || community == null || ActiveTrade != null || outgoingRecipient.HasValue || WriteBlocked
            || SeasonReadOnly || community.MyTradingRestricted || target.TradingRestricted || target.Character.Id == session.Character.Id
            || target.Status != "active")
        {
            tradeRequestMessage = "This trade request could not be sent. Check both players’ trading access and any pending request.";
            return;
        }
        outgoingRecipient = target.Character.Id;
        tradeRequestMessage = "Sending request to " + target.Character.DisplayName + "…";
        Mutate("POST", $"api/v2/seasons/{community.Id}/trades", new TradeCreate(target.Character.Id));
    }

    public void TradeWindowOpened()
    {
        ClearTransientFeedback(); tradeWindowVisible = true; tradeWindowCloseRequested = false; nextTradePoll = default;
    }
    public void TradeWindowClosed()
    {
        tradeWindowVisible = false; tradeWindowCloseRequested = true; tradeAcceptRequested = false;
        if (ActiveTrade is { State: "open" or "confirming" }) RequestTradeCancel();
    }
    private void RequestTradeCancel()
    {
        tradeCancelRequested = true; tradeReadyRequested = tradeAcceptRequested = false;
        finalReview = null; tradeFeedback = "Cancelling trade…";
    }
    private void ClearTradeViews()
    {
        tradeRequestTarget = null; outgoingRecipient = null; resolvingTradeTarget = false; tradeRequestMessage = "";
        SetTradeRequestWindow?.Invoke(false);
        trades = []; tradeInventory = []; tradeInventoryCursor = null;
        tradeSearch = tradeInventoryQuery = ""; tradeQuantityMove = null; tradeSelection = null;
        draftTradeHoldings.Clear(); tradeInventoryLoaded = openTradeQuantity = false;
        tradeDraft = null; finalReview = null; popupForTrade = null; submittedTradeGeneration = 0;
        tradeCancelRequested = tradeReadyRequested = tradeAcceptRequested = tradePreparing = confirmationStartedLocally = false;
        tradeConflictRetries = 0; tradeFeedback = tradePollError = ""; switchAfterTrade = null; nextTradePoll = default;
        if (tradeWork != null) _ = tradeWork.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default); tradeWork = null;
    }
    private void ObserveTrade(Trade t)
    {
        var previous = trades.FirstOrDefault(x => x.Id == t.Id);
        if (previous != null && (previous.Revision > t.Revision || !Active(previous) && Active(t))) return;
        if (previous != null && previous.Revision == t.Revision && Active(previous) && Active(t))
        {
            t = t with {
                Initiator = t.Initiator with { Confirmed = t.Initiator.Confirmed || previous.Initiator.Confirmed, FinalConfirmed = t.Initiator.FinalConfirmed || previous.Initiator.FinalConfirmed },
                Recipient = t.Recipient with { Confirmed = t.Recipient.Confirmed || previous.Recipient.Confirmed, FinalConfirmed = t.Recipient.FinalConfirmed || previous.Recipient.FinalConfirmed },
                State = previous.State == "confirming" ? "confirming" : t.State
            };
        }
        trades = new[] { t }.Concat(trades.Where(x => x.Id != t.Id)).ToArray();
        tradeInbox.Observe(t.CommunityId, t);
        if (Active(t)) tradeSelection = t.Id;
        if (session == null) return;
        var isOutgoingRequest = t.Initiator.Character.Id == session.Character.Id && t.Recipient.Character.Id == outgoingRecipient
            && (Active(t) || previous == null && !HasUnresolvedAction);
        if (t.State is "open" or "confirming" && (previous?.State == "invited" || previous == null && isOutgoingRequest)
            && !tradeWindowCloseRequested && !tradeCancelRequested)
        {
            tradeRequestMessage = "";
            SetTradeRequestWindow?.Invoke(false);
            OpenTradeWindow?.Invoke();
        }
        if (isOutgoingRequest) outgoingRecipient = null;
        if (tradeDraft?.TradeId != t.Id && SelectedTrade?.Id == t.Id)
        {
            tradeDraft = new(t, session.Character.Id);
            draftTradeHoldings.Clear();
            confirmationStartedLocally = false;
            popupForTrade = null;
        }
        else if (tradeDraft?.TradeId == t.Id) tradeDraft.Observe(t, session.Character.Id);
        if (!Active(t))
        {
            if (previous?.State == "invited" || isOutgoingRequest)
                tradeRequestMessage = t.State == "expired" ? "Trade request expired."
                    : t.State == "cancelled" ? "Trade request cancelled."
                    : t.EndReason ?? "The trade request ended.";
            if (tradeSelection == t.Id && previous != null && Active(previous)) tradeOutcomeUntil = DateTimeOffset.UtcNow + FeedbackLifetime;
            if (tradeDraft?.TradeId == t.Id) { finalReview = null; tradeCancelRequested = tradeReadyRequested = tradeAcceptRequested = false; confirmationStartedLocally = false; }
            if (switchAfterTrade != null) { var next = switchAfterTrade; switchAfterTrade = null; SwitchCommunity(next); }
        }
    }
    private void TickTrades()
    {
        if (tradeWork is { IsCompleted: true })
        {
            var done = tradeWork; tradeWork = null;
            if (tradeView == viewGeneration && tradeData == dataGeneration)
            {
                if (done.IsCompletedSuccessfully) { tradePollError = ""; done.Result(); }
                else
                {
                    var error = done.Exception?.GetBaseException(); tradeFeedback = error is FoundationApiException ? error.Message : "Trade sync unavailable. The last confirmed offer is still shown.";
                    if (feedback.Latest("trade") is { } message) message.Details = UserFeedback.Details(error);
                    tradePollError = "Trade notifications could not refresh. Retrying shortly.";
                    nextTradePoll = DateTimeOffset.UtcNow.AddSeconds(30);
                    if (error is FoundationApiException api && api.Status == System.Net.HttpStatusCode.Unauthorized) { ClearCommunityViews(); session = null; }
                    else if (error is FoundationApiException lost && lost.Status is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden && community != null)
                    { tradeInbox.Observe(community.Id, null); refreshNeeded = true; }
                }
            }
            else { _ = done.Exception; nextTradePoll = default; }
        }
        PumpTradeChanges();
        if (tradeWork != null || session == null || community == null || session.AccessExpiresAt <= DateTimeOffset.UtcNow || DateTimeOffset.UtcNow < nextTradePoll) return;
        var uri = origin!; var token = session.AccessToken; var id = community.Id; var ct = requests.Token;
        var loadInventory = tradeWindowVisible; var inventoryCount = tradeInventory.Length;
        var inventoryQuery = tradeInventoryQuery;
        tradeView = viewGeneration; tradeData = dataGeneration;
        nextTradePoll = DateTimeOffset.UtcNow.AddSeconds(ActiveTrade != null || tradeWindowVisible ? 2 : 15);
        tradeWork = Task.Run(async () => {
            // Only the latest trade is needed for invitation/active recovery. Old outcomes
            // remain in history; do not repeatedly download full historical item previews.
            var pageTrades = await client.GetAsync<TradePage>(uri, $"api/v2/seasons/{id}/trades?limit=1", token, ct);
            var balances = loadInventory ? await client.GetAsync<AmorRP.Contracts.Currency.BalancePage>(uri, $"api/v2/seasons/{id}/members/{session.Character.Id}/balances", token, ct) : null;
            HoldingPage? inventory = loadInventory ? await client.GetAsync<HoldingPage>(uri, $"api/v2/seasons/{id}/inventory{inventoryQuery}", token, ct) : null;
            if (inventory != null && inventoryCount > inventory.Items.Length)
            {
                var list = inventory.Items.ToList();
                while (inventory.NextCursor != null && list.Count < inventoryCount)
                { var page = await client.GetAsync<HoldingPage>(uri, TradeInventoryPath(id, inventoryQuery, inventory.NextCursor), token, ct); list.AddRange(page.Items); inventory = page; }
                inventory = inventory with { Items = list.ToArray() };
            }
            return (Action)(() => {
                if (balances?.Items.FirstOrDefault() is { } balance && community?.Id == id && balance.Version >= community.MyBalance.Version)
                    community = community with { MyBalance = balance };
                if (inventory != null && tradeInventoryQuery == inventoryQuery)
                {
                    tradeInventory = inventory.Items;
                    tradeInventoryCursor = inventory.NextCursor;
                    tradeInventoryLoaded = true;
                }
                var previous = ActiveTrade;
                foreach (var t in pageTrades.Items) ObserveTrade(t);
                if (previous != null && ActiveTrade == null)
                { tradeFeedback = "Trade ended. Inventory and balance are updating."; RefreshAffected(RefreshTargets.Inventory); }
            });
        }, ct);
    }
    private void PumpContextTargetResolution()
    {
        if (tradeRequestTarget is not { } target || resolvingTradeTarget || community == null || session == null
            || origin == null || WriteBlocked || Busy || SyncBusy || refreshNeeded
            || session.AccessExpiresAt <= DateTimeOffset.UtcNow) return;

        var seasonId = community.Id;
        var uri = origin;
        var token = session.AccessToken;
        resolvingTradeTarget = true;
        Start(async ct => {
            var match = await FindSeasonCharacterAsync(uri, token, seasonId, target, ct);
            return () => {
                resolvingTradeTarget = false;
                if (!ReferenceEquals(tradeRequestTarget, target) || community?.Id != seasonId) return;
                tradeRequestTarget = null;
                if (match == null) tradeRequestMessage = "That character isn’t an active member of this season.";
                else SendTradeRequest(match);
            };
        }, "Resolving season member…");
    }

    // Preparation reads are separate from command submission. A rejected stale write
    // can be rebased only after a definitive rejection; uncertain writes keep their key.
    private void PumpTradeChanges()
    {
        var t = SelectedTrade;
        if (!tradeWindowVisible && !tradeCancelRequested && !tradeAcceptRequested) return;
        if (t == null || !Active(t) || session == null || origin == null || community == null || WriteBlocked || Busy || tradePreparing) return;
        var action = tradeCancelRequested ? "cancel" : tradeAcceptRequested ? "accept" : tradeDraft?.Dirty == true && !tradeDraft.CurrencyFocused && tradeDraft.Error == null ? "offer" : tradeReadyRequested ? "confirm" : null;
        if (action == null) return;
        var uri = origin; var token = session.AccessToken; var path = $"api/v2/seasons/{t.CommunityId}/trades/{t.Id}";
        tradePreparing = true;
        Start(async ct => {
            var fresh = await client.GetAsync<Trade>(uri, path, token, ct);
            return () => {
                tradePreparing = false;
                if (SelectedTrade?.Id != fresh.Id) return;
                // Evaluate against the draft's original own-side baseline before Observe.
                var canRebase = tradeDraft?.CanRebase(fresh, session!.Character.Id) == true;
                ObserveTrade(fresh);
                if (!Active(fresh)) { tradeFeedback = fresh.EndReason ?? "Trade ended."; return; }
                if (tradeCancelRequested || action == "offer" && Reviewing(fresh))
                { tradeCancelRequested = true; tradeReadyRequested = tradeAcceptRequested = false; Mutate("POST", path + "/cancel", new TradeRevision(fresh.Revision)); return; }
                if (action == "offer")
                {
                    if (!canRebase) { tradeDraft!.AdoptExternal(fresh, session.Character.Id); tradeFeedback = tradeDraft.Error!; return; }
                    submittedTradeGeneration = tradeDraft!.Generation;
                    Mutate("PUT", path + "/offers/mine", tradeDraft.Request(fresh));
                }
                else if (action == "accept")
                {
                    tradeAcceptRequested = false;
                    if (fresh.State == "invited" && fresh.Recipient.Character.Id == session.Character.Id)
                        Mutate("POST", path + "/accept", new TradeRevision(fresh.Revision));
                }
                else
                {
                    if (tradeDraft?.CurrencyFocused == true || tradeDraft?.Dirty == true) return;
                    tradeReadyRequested = false;
                    if (fresh.State != "open") return;
                    confirmationStartedLocally = true;
                    Mutate("POST", path + "/confirm", new TradeRevision(fresh.Revision));
                }
            };
        }, "Updating trade…");
    }
    private void NoteTradeFailure(CommandRequest? request, Exception? failure)
    {
        var preparing = tradePreparing;
        tradePreparing = false;
        if (request == null)
        {
            if (resolvingTradeTarget)
            {
                resolvingTradeTarget = false;
                if (tradeRequestTarget != null) tradeRequestMessage = status;
                tradeRequestTarget = null;
            }
            if (preparing && !tradeWindowVisible && (tradeAcceptRequested || tradeCancelRequested))
            {
                tradeAcceptRequested = tradeCancelRequested = false;
                tradeRequestMessage = status;
                nextTradePoll = default;
            }
            return;
        }
        if (request.Operation.Handler != CommandHandler.Trade) return;
        if (request.Operation.Kind is CommandKind.CreateTrade or CommandKind.AcceptTrade or CommandKind.CancelTrade)
        {
            tradeRequestMessage = status;
            if (request.Operation.Kind == CommandKind.CreateTrade && failure is FoundationApiException { OutcomeUncertain: false })
                outgoingRecipient = null;
            if (request.Operation.Kind == CommandKind.CancelTrade && failure is FoundationApiException { OutcomeUncertain: false })
                tradeCancelRequested = false;
        }
        tradeFeedback = status;
        if (failure is FoundationApiException { Code: "trade_changed", OutcomeUncertain: false } && ++tradeConflictRetries <= 3)
        {
            if (request.Operation.Kind == CommandKind.ConfirmTrade) { tradeReadyRequested = true; confirmationStartedLocally = false; }
            nextTradePoll = default; tradeFeedback = "The other offer changed; synchronizing automatically.";
        }
        else { tradeReadyRequested = false; if (tradeDraft != null) tradeDraft.Pause(status); }
    }
    private void ApplyTradeSuccess(CommandRequest request, string json)
    {
        if (request.Operation.Handler != CommandHandler.Trade) return;
        var t = JsonSerializer.Deserialize<CommandResult<Trade>>(json, FoundationClient.Json)!.Result;
        if (request.Operation.Kind == CommandKind.EditTradeOffer && tradeDraft?.TradeId == t.Id) tradeDraft.Acknowledge(t, session!.Character.Id, submittedTradeGeneration);
        ObserveTrade(t); tradeSelection = t.Id; tradeConflictRetries = 0;
        if (tradeWindowCloseRequested && t.State is "open" or "confirming") RequestTradeCancel();
        if (!Active(t)) tradeOutcomeUntil = DateTimeOffset.UtcNow + FeedbackLifetime;
        tradeFeedback = "";
        actionResult = t.State == "completed" ? "Trade completed. Items and currency have been exchanged." : t.State == "cancelled" ? t.EndReason ?? "Trade cancelled." : UserFeedback.Success(request);
        if (request.Operation.Kind == CommandKind.CreateTrade)
        {
            outgoingRecipient = null;
            if (ActiveTrade?.State == "invited") tradeRequestMessage = "";
        }
        nextTradePoll = default;
    }
}
