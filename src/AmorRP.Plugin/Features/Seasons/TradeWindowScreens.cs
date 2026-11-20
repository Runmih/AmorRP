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
            DrawTradeRequests();
        }
        ImGui.EndChild();
    }
    private void DrawTradeRequests()
    {
        if (!ImGui.BeginTabBar("TradeRequests")) return;
        if (ImGui.BeginTabItem("Inbound", requestedTradeTab == 0 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
        {
            var requests = tradeInbox.Pending(DateTimeOffset.UtcNow);
            if (requests.Length == 0)
            {
                Wrapped("No incoming trade requests.");
                Wrapped("To start a trade, right-click a character and choose Amor RP: Trade in the character context menu.");
            }
            foreach (var request in requests)
            {
                var targetCommunity = communities.FirstOrDefault(x => x.Id == request.CommunityId);
                if (targetCommunity == null) continue;
                ImGui.PushID(request.Id.ToString());
                Wrapped($"{request.Initiator.Character.DisplayName} @ {request.Initiator.Character.HomeWorldName} · {targetCommunity.Name}");
                Wrapped("Expires in " + TradeCountdown(request.ExpiresAt));
                ImGui.BeginDisabled(Busy || tradeAcceptRequested || tradePreparing || (request.CommunityId != community!.Id && TradeBlocksCommunitySwitch));
                if (ActionButton("Accept")) AcceptIncomingTrade(request);
                ImGui.EndDisabled(); ImGui.Separator(); ImGui.PopID();
            }
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Outbound", requestedTradeTab == 1 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
        {
            var outgoing = ActiveTrade;
            if (outgoing is { State: "invited" } && outgoing.Initiator.Character.Id == session!.Character.Id)
            {
                Wrapped($"Request sent to {outgoing.Recipient.Character.DisplayName} @ {outgoing.Recipient.Character.HomeWorldName}. Waiting to be accepted…");
                Wrapped(outgoing.ExpiresAt > DateTimeOffset.UtcNow ? "Expires in " + TradeCountdown(outgoing.ExpiresAt) : "Request expired. Updating…");
                ImGui.BeginDisabled(tradeCancelRequested || tradePreparing);
                if (ActionButton("Cancel request")) RequestTradeCancel();
                ImGui.EndDisabled();
            }
            else
            {
                Wrapped("To start a trade, right-click a character and choose Amor RP: Trade in the character context menu.");
                DrawTradeTarget();
            }
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar(); requestedTradeTab = null;
    }
    private static string TradeCountdown(DateTimeOffset expiresAt)
    {
        var seconds = Math.Max(0, (long)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
    private void AcceptIncomingTrade(Trade request)
    {
        if (session == null || request.State != "invited" || request.Recipient.Character.Id != session.Character.Id
            || request.ExpiresAt <= DateTimeOffset.UtcNow || WriteBlocked || Busy) return;
        if (request.CommunityId != community?.Id)
        {
            if (TradeBlocksCommunitySwitch) return;
            var target = communities.FirstOrDefault(x => x.Id == request.CommunityId);
            if (target == null) return;
            SwitchCommunity(target);
        }
        contextTarget = null; contextLookup = null; tradeTargetId = null;
        ObserveTrade(request); tradeSelection = request.Id; tradeAcceptRequested = true; nextTradePoll = default;
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
    private void DrawTradeTarget()
    {
        if (ActiveTrade != null) return;
        var chosen = members.FirstOrDefault(x => x.Status == "active" && x.Character.Id == tradeTargetId && x.Character.Id != session!.Character.Id);
        if (chosen == null)
        {
            if (contextTarget != null) Wrapped("Selected character: " + contextTarget.Name);
            return;
        }
        Wrapped("Trade with " + chosen.Character.DisplayName + " @ " + chosen.Character.HomeWorldName);
        ImGui.BeginDisabled(community!.MyTradingRestricted || chosen.TradingRestricted);
        if (ActionButton("Send request")) Mutate("POST", $"api/v2/seasons/{community!.Id}/trades", new TradeCreate(chosen.Character.Id));
        ImGui.EndDisabled();
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
