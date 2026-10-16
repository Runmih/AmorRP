using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    public void DrawTradeWindow()
    {
        if (session == null || group == null)
        {
            Wrapped("Sign in and select the shared group in the main window first.");
            if (ImGui.Button("Open Amor RP")) OpenMainWindow?.Invoke();
            return;
        }
        Wrapped("Group: " + group.Name);
        if (TradeNotificationStatus.Length > 0) Wrapped(TradeNotificationStatus);
        var pending = configuration.PendingCommands.FirstOrDefault(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session.Character.Id);
        var footerHeight = ActionFeedbackHeight(pending);
        ImGui.BeginChild("TradeContent", new(0, footerHeight > 0 ? -footerHeight - ImGui.GetStyle().ItemSpacing.Y : 0), false);
        var t = SelectedTrade;
        if (t is { State: "open" or "confirming" }) DrawActiveTrade(t);
        else
        {
            CloseFinalPopup();
            DrawTradeRequests();
        }
        ImGui.EndChild(); DrawActionFeedback(pending);
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
                var targetGroup = groups.FirstOrDefault(x => x.Id == request.GroupId);
                if (targetGroup == null) continue;
                ImGui.PushID(request.Id.ToString());
                Wrapped($"{request.Initiator.Character.DisplayName} @ {request.Initiator.Character.HomeWorldName} · {targetGroup.Name}");
                Wrapped("Expires in " + TradeCountdown(request.ExpiresAt));
                ImGui.BeginDisabled(work != null || tradeAcceptRequested || tradePreparing || (request.GroupId != group!.Id && TradeBlocksGroupSwitch));
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
            || request.ExpiresAt <= DateTimeOffset.UtcNow || WriteBlocked || work != null) return;
        if (request.GroupId != group?.Id)
        {
            if (TradeBlocksGroupSwitch) return;
            var target = groups.FirstOrDefault(x => x.Id == request.GroupId);
            if (target == null) return;
            SwitchGroup(target);
        }
        contextTarget = null; contextLookup = null; tradeTargetId = null;
        ObserveTrade(request); tradeSelection = request.Id; tradeAcceptRequested = true; nextTradePoll = default;
    }
    private void DrawActiveTrade(Trade t)
    {
        var session = this.session!; var group = this.group!;
        Wrapped("Build your offer with the arrows. Currency saves when you leave its field.");
        tradeDraft ??= new(t, session.Character.Id);
        var own = TradeDraft.Own(t, session.Character.Id); var other = t.Initiator.Character.Id == session.Character.Id ? t.Recipient : t.Initiator;
        ImGui.SetNextItemWidth(140); ImGui.InputInt("Quantity for arrows", ref transferQuantity); transferQuantity = Math.Max(1, transferQuantity);
        if (ImGui.BeginTable("TradeInventoryAndOffers", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            ImGui.BeginChild("TradeInventory", new System.Numerics.Vector2(0, Math.Max(180, ImGui.GetContentRegionAvail().Y - 80)), true);
            ImGui.TextUnformatted("Your inventory");
            foreach (var item in tradeInventory)
            {
                ImGui.PushID(item.Id.ToString());
                var inDraft = tradeDraft.Items.GetValueOrDefault(item.Id); var saved = own.Items.FirstOrDefault(x => x.HoldingId == item.Id)?.Quantity ?? 0;
                var available = Math.Max(0, item.Available + saved - inDraft);
                ImGui.BeginDisabled(available == 0);
                if (ImGui.ArrowButton("AddToOffer", ImGuiDir.Right)) ChangeOfferItem(t, item.Id, inDraft + Math.Min(transferQuantity, available));
                ImGui.EndDisabled(); ImGui.SameLine();
                ImGui.TextUnformatted($"{item.Name} × {available}");
                ImGui.PopID();
            }
            if (tradeInventoryCursor != null && ImGui.SmallButton("More inventory"))
            {
                var uri = origin!; var token = session.AccessToken; var path = $"api/v1/groups/{group.Id}/inventory?cursor=" + Uri.EscapeDataString(tradeInventoryCursor);
                Start(async ct => { var page = await client.GetAsync<AmorRP.Contracts.Inventory.HoldingPage>(uri, path, token, ct); return () => { tradeInventory = tradeInventory.Concat(page.Items).DistinctBy(x => x.Id).ToArray(); tradeInventoryCursor = page.NextCursor; }; }, "Reading inventory…");
            }
            ImGui.EndChild();
            ImGui.TableNextColumn();
            var areaHeight = Math.Max(180, ImGui.GetContentRegionAvail().Y - 80);
            ImGui.BeginChild("MyOffer", new System.Numerics.Vector2(0, areaHeight / 2), true);
            ImGui.TextUnformatted("You offer");
            ImGui.SetNextItemWidth(Math.Min(200, ImGui.GetContentRegionAvail().X));
            var currencyChanged = ImGui.InputText(group.Currency.Name, ref tradeDraft.CurrencyText, 64);
            tradeDraft.CurrencyFocused = ImGui.IsItemActive();
            if (currencyChanged && (Reviewing(t) || tradeReadyRequested)) RequestTradeCancel();
            if (ImGui.IsItemDeactivatedAfterEdit()) { if (tradeDraft.CommitCurrency()) tradeConflictRetries = 0; }
            if (tradeDraft.Error != null) Wrapped(tradeDraft.Error);
            foreach (var line in tradeDraft.Items.ToArray())
            {
                var name = tradeInventory.FirstOrDefault(x => x.Id == line.Key)?.Name ?? own.Items.FirstOrDefault(x => x.HoldingId == line.Key)?.Name ?? "Item";
                ImGui.PushID(line.Key.ToString());
                if (ImGui.ArrowButton("RemoveFromOffer", ImGuiDir.Left)) ChangeOfferItem(t, line.Key, line.Value - Math.Min(transferQuantity, line.Value));
                ImGui.SameLine(); ImGui.TextUnformatted($"{name} × {line.Value}"); ImGui.PopID();
            }
            ImGui.TextUnformatted(tradeDraft.Dirty ? "Updating offer…" : own.Confirmed ? "Accepted" : "Offer saved");
            ImGui.EndChild();
            ImGui.BeginChild("OtherOffer", new System.Numerics.Vector2(0, areaHeight / 2), true);
            ImGui.TextUnformatted(other.Character.DisplayName + " offers");
            DrawOffer(other); ImGui.EndChild(); ImGui.EndTable();
        }
        if (own.Confirmed) ImGui.TextUnformatted(t.State == "confirming" ? "Final review required" : "Accepted; waiting for the other player");
        else if (ImGui.Button("Accept offer"))
        {
            if (!tradeDraft.CommitCurrency()) return;
            tradeReadyRequested = true;
        }
        ImGui.SameLine(); if (ImGui.Button("Cancel trade")) RequestTradeCancel();
        ImGui.TextUnformatted($"Expires {t.ExpiresAt.LocalDateTime:T}. Editing after either acceptance cancels this trade.");
        if (switchAfterTrade != null) Wrapped("Cancel this trade to switch to " + switchAfterTrade.Name + ".");
        if (t.State == "confirming" && popupForTrade != t.Id)
        { finalReview = t; popupForTrade = t.Id; ImGui.OpenPopup("Final trade confirmation"); }
        DrawFinalConfirmation(t);
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
        ImGui.BeginDisabled(group!.MyTradingRestricted || chosen.TradingRestricted);
        if (ActionButton("Send request")) Mutate("POST", $"api/v1/groups/{group!.Id}/trades", new TradeCreate(chosen.Character.Id));
        ImGui.EndDisabled();
    }
    private void DrawFinalConfirmation(Trade current)
    {
        if (!ImGui.BeginPopupModal("Final trade confirmation", ImGuiWindowFlags.AlwaysAutoResize)) return;
        if (current.State != "confirming" || finalReview?.Id != current.Id || finalReview.Revision != current.Revision || tradeCancelRequested)
        { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); return; }
        var review = finalReview;
        var own = TradeDraft.Own(review, session!.Character.Id); var other = review.Initiator.Character.Id == session.Character.Id ? review.Recipient : review.Initiator;
        Wrapped("Group: " + group!.Name);
        ImGui.TextUnformatted("Both players accepted. Review the exact exchange below.");
        ImGui.BeginChild("FinalOfferSummary", new System.Numerics.Vector2(520, 320), true);
        ImGui.TextUnformatted("You send"); DrawOffer(own); ImGui.Separator();
        ImGui.TextUnformatted("You receive"); DrawOffer(other); ImGui.EndChild();
        Wrapped("Editing, cancelling or closing the trade ends this deal. Nothing transfers until both players confirm here.");
        var ownCurrent = TradeDraft.Own(current, session.Character.Id);
        if (ownCurrent.FinalConfirmed) ImGui.TextUnformatted("Your final confirmation is saved. Waiting for the other player.");
        else if (ActionButton("Confirm exchange")) Mutate("POST", $"api/v1/groups/{current.GroupId}/trades/{current.Id}/final-confirm", new TradeRevision(review.Revision));
        ImGui.SameLine(); if (ImGui.Button("Cancel entire trade")) { RequestTradeCancel(); ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }
    private void CloseFinalPopup()
    {
        if (ImGui.BeginPopupModal("Final trade confirmation", ImGuiWindowFlags.AlwaysAutoResize)) { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); }
    }
    private void DrawOffer(TradeOffer offer)
    {
        Wrapped(offer.CurrencyAmount + " " + group!.Currency.Name);
        foreach (var item in offer.Items)
        {
            Wrapped($"{item.Quantity} × {item.Name} ({item.TypeId})");
            if (item.DefinitionRevision.HasValue) Wrapped("Recipe revision: " + item.DefinitionRevision);
            if (item.Description != null) Wrapped(item.Description);
            if (item.UseMessage != null) Wrapped("Use message: " + item.UseMessage);
            if (item.Author != null) Wrapped($"Sealed text · author {item.Author.DisplayName} @ {item.Author.HomeWorldName}");
        }
        Wrapped(offer.FinalConfirmed ? "Final confirmation saved" : offer.Confirmed ? "Accepted" : "Editing offer");
    }
}
