using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    public void DrawTradeWindow()
    {
        if (session == null || community == null)
        {
            Wrapped(session == null ? "Sign in from Settings to trade." : "Select the shared season in Home to trade.");
            if (ImGui.Button(session == null ? "Open Settings" : "Open Home"))
            {
                if (session == null) { requestedAccountSettings = true; OpenSettingsWindow?.Invoke(); }
                else OpenWorkspace(AmorRP.Plugin.Features.Shell.WorkspaceKind.Season);
            }
            return;
        }
        Wrapped("Season: " + community.Name);
        if (TradeNotificationStatus.Length > 0) Wrapped(TradeNotificationStatus);
        ImGui.BeginChild("TradeContent", System.Numerics.Vector2.Zero, false);
        var t = SelectedTrade;
        if (t is { State: "open" or "confirming" }) DrawActiveTrade(t);
        else
        {
            CloseFinalPopup();
            CloseTradeQuantity();
            if (t is { } ended && !Active(ended)) Wrapped(ended.EndReason ?? (ended.State == "completed" ? "Trade completed." : "Trade ended."));
        }
        ImGui.EndChild();
    }

    public void DrawTradeRequestWindow()
    {
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetFontSize() * 25);
        DrawTradeRequestContent();
        ImGui.PopTextWrapPos();
    }

    private void DrawTradeRequestContent()
    {
        if (tradeRequestTarget != null)
        {
            ImGui.TextWrapped("Finding " + tradeRequestTarget.Name + "…");
            if (ImGui.Button("Cancel")) { tradeRequestTarget = null; SetTradeRequestWindow?.Invoke(false); }
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (ActiveTrade is { State: "invited" } outgoing && outgoing.Initiator.Character.Id == session?.Character.Id)
        {
            ImGui.TextWrapped($"Request sent to {outgoing.Recipient.Character.DisplayName}. Awaiting response.");
            ImGui.TextUnformatted(outgoing.ExpiresAt > now ? "Expires in " + TradeCountdown(outgoing.ExpiresAt) : "Request expired.");
            ImGui.BeginDisabled(tradeCancelRequested || tradePreparing || outgoing.ExpiresAt <= now);
            if (ActionButton("Cancel request")) RequestTradeCancel();
            ImGui.EndDisabled();
            if (tradeCancelRequested) ImGui.TextWrapped("Cancelling request…");
        }
        else
        {
            var requests = tradeInbox.Pending(now);
            foreach (var request in requests)
            {
                ImGui.PushID(request.Id.ToString());
                ImGui.TextWrapped(request.Initiator.Character.DisplayName + " wants to trade.");
                ImGui.TextUnformatted("Expires in " + TradeCountdown(request.ExpiresAt));
                ImGui.BeginDisabled(Busy || outgoingRecipient.HasValue || tradeAcceptRequested || tradeCancelRequested || tradePreparing
                    || request.CommunityId != community?.Id && TradeBlocksCommunitySwitch);
                if (ActionButton("Accept")) RespondToTradeRequest(request, accept: true);
                ImGui.SameLine();
                if (ActionButton("Decline")) RespondToTradeRequest(request, accept: false);
                ImGui.EndDisabled();
                ImGui.PopID();
            }
            if (requests.Length == 0 && tradeRequestMessage.Length == 0)
                ImGui.TextWrapped("Right-click a player and choose ♥ Amor RP → Trade.");
        }

        if (tradeRequestMessage.Length > 0) ImGui.TextWrapped(tradeRequestMessage);
        if (HasPendingAction) ImGui.TextWrapped("Check the pending action in the Activity Log.");
        if (TradeNotificationStatus.Length > 0) ImGui.TextWrapped(TradeNotificationStatus);
    }
    private static string TradeCountdown(DateTimeOffset expiresAt)
    {
        var seconds = Math.Max(0, (long)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
    private void RespondToTradeRequest(Trade request, bool accept)
    {
        if (session == null || request.State != "invited" || request.Recipient.Character.Id != session.Character.Id
            || request.ExpiresAt <= DateTimeOffset.UtcNow || WriteBlocked || Busy) return;
        if (request.CommunityId != community?.Id)
        {
            if (TradeBlocksCommunitySwitch) return;
            var target = communities.FirstOrDefault(x => x.Id == request.CommunityId);
            if (target == null) return;
            SwitchCommunity(target);
            SetTradeRequestWindow?.Invoke(true);
        }
        tradeWindowCloseRequested = false;
        ObserveTrade(request);
        tradeSelection = request.Id;
        tradeRequestMessage = accept ? "Accepting request…" : "Declining request…";
        if (accept) tradeAcceptRequested = true;
        else RequestTradeCancel();
        nextTradePoll = default;
    }
    private void DrawActiveTrade(Trade t)
    {
        var session = this.session!;
        tradeDraft ??= new(t, session.Character.Id);
        var own = TradeDraft.Own(t, session.Character.Id);
        var other = t.Initiator.Character.Id == session.Character.Id ? t.Recipient : t.Initiator;
        DrawTradeBalance(own);
        DrawTradeGrids(t, own, other);
        if (own.Confirmed) ImGui.TextUnformatted(t.State == "confirming" ? "Final review required" : "Accepted; waiting for the other player");
        else if (ImGui.Button("Accept offer"))
        {
            if (!tradeDraft.CommitCurrency()) return;
            tradeReadyRequested = true;
        }
        ImGui.SameLine(); if (ImGui.Button("Cancel trade")) RequestTradeCancel();
        ImGui.TextUnformatted($"Expires {t.ExpiresAt.LocalDateTime:T}. Offers lock when either player accepts.");
        if (switchAfterTrade != null) Wrapped("Cancel this trade to switch to " + switchAfterTrade.Name + ".");
        DrawTradeQuantity(t, own);
        if (t.State == "confirming" && popupForTrade != t.Id)
        { finalReview = t; popupForTrade = t.Id; ImGui.OpenPopup("Final trade confirmation"); }
        DrawFinalConfirmation(t);
    }
    private void DrawTradeBalance(TradeOffer own)
    {
        var balance = community!.MyBalance;
        Wrapped($"Your balance: {balance.Owned} {community.Currency.Name}");
        if (System.Numerics.BigInteger.TryParse(balance.Available, out var available) && System.Numerics.BigInteger.TryParse(own.CurrencyAmount, out var offered))
            Wrapped($"Available for this offer: {available + offered} {community.Currency.Name}");
        Wrapped("In your offer: " + (tradeDraft?.CurrencyText ?? own.CurrencyAmount) + " " + community.Currency.Name);
        ImGui.Separator();
    }
    private void ChangeOfferItem(Trade t, Guid id, int quantity)
    {
        if (Reviewing(t) || tradeReadyRequested) { RequestTradeCancel(); return; }
        if (quantity > 0 && tradeDraft!.Items.GetValueOrDefault(id) == 0 && tradeDraft.Items.Count >= (serviceLimits?.MaxTradeLinesPerSide ?? 20))
        { tradeFeedback = "The offer has reached its item-line limit."; return; }
        tradeDraft!.ChangeItem(id, quantity); tradeConflictRetries = 0;
    }
    private void DrawFinalConfirmation(Trade current)
    {
        if (!ImGui.BeginPopupModal("Final trade confirmation", ImGuiWindowFlags.AlwaysAutoResize)) return;
        if (current.State != "confirming" || finalReview?.Id != current.Id || finalReview.Revision != current.Revision || tradeCancelRequested)
        { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); return; }
        if (TradeDraft.Own(current, session!.Character.Id).FinalConfirmed)
            ImGui.TextUnformatted("Waiting for the other player to confirm.");
        else
        {
            ImGui.TextUnformatted("Confirm this trade?");
            if (ActionButton("Confirm"))
                Mutate("POST", $"api/v2/seasons/{current.CommunityId}/trades/{current.Id}/final-confirm", new TradeRevision(finalReview.Revision));
            ImGui.SameLine();
        }
        if (ImGui.Button("Cancel trade")) { RequestTradeCancel(); ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }
    private void CloseFinalPopup()
    {
        if (ImGui.BeginPopupModal("Final trade confirmation", ImGuiWindowFlags.AlwaysAutoResize)) { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); }
    }
}
