using System.Numerics;
using System.Runtime.InteropServices;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private const string FromInventory = "AMORRP_TRADE_INVENTORY";
    private const string FromOffer = "AMORRP_TRADE_OFFER";
    private const string QuantityPopup = "Trade quantity";
    private readonly Dictionary<Guid, Holding> draftTradeHoldings = new();
    private string tradeSearch = "", tradeInventoryQuery = "";
    private (Guid TradeId, Guid HoldingId, bool Add)? tradeQuantityMove;
    private int tradeMoveQuantity;
    private bool openTradeQuantity;

    private void DrawTradeGrids(Trade trade, TradeOffer own, TradeOffer other)
    {
        var lineLimit = serviceLimits?.MaxTradeLinesPerSide ?? 20;
        var cell = ImGui.GetFrameHeight() * 1.65f;
        var narrow = ImGui.GetContentRegionAvail().X < cell * 12;
        var editable = !WriteBlocked && !tradeCancelRequested && !tradeReadyRequested && !Reviewing(trade) && trade.State == "open";
        var available = ImGui.GetContentRegionAvail().Y;
        var height = narrow ? Math.Max(cell * 9, available - ImGui.GetFrameHeightWithSpacing() * 4)
            : Math.Max(cell * 5, available - ImGui.GetFrameHeightWithSpacing() * 4);
        if (!ImGui.BeginTable("TradeInventoryAndOffers", narrow ? 1 : 2, ImGuiTableFlags.SizingStretchSame)) return;

        ImGui.TableNextColumn();
        ImGui.BeginChild("TradeInventory", new(0, narrow ? height * .34f : height), true);
        ImGui.TextUnformatted("Your inventory");
        if (editable) Wrapped("Drag items between these grids or right-click for actions.");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##TradeInventorySearch", "Search items…", ref tradeSearch, 100, ImGuiInputTextFlags.EnterReturnsTrue)
            || ImGui.IsItemDeactivatedAfterEdit())
        {
            var search = tradeSearch.Trim();
            var query = search.Length == 0 ? "" : "?search=" + Uri.EscapeDataString(search);
            if (tradeInventoryQuery != query)
            {
                tradeInventoryQuery = query;
                tradeInventory = [];
                tradeInventoryCursor = null;
                tradeInventoryLoaded = false;
                nextTradePoll = default;
            }
        }
        if (tradeInventory.Length == 0 && tradeInventoryLoaded)
            ImGui.TextDisabled(tradeInventoryQuery.Length == 0 ? "Inventory is empty." : "No matching items.");
        var columns = TradeColumns(cell);
        for (var i = 0; i < tradeInventory.Length; i++)
        {
            var item = tradeInventory[i];
            StartTradeCell(i, columns);
            var quantity = AvailableForTrade(own, item);
            DrawTradeCell(item.Id, item.Name, item.TypeId, quantity,
                editable && quantity > 0 ? FromInventory : null, trade.Id,
                editable && quantity > 0 ? () => RequestTradeQuantity(trade, own, item.Id, true) : null);
        }
        if (tradeInventoryCursor != null && ActionButton("More inventory", mutation: false)) LoadMoreTradeInventory(trade);
        ImGui.EndChild();
        if (editable) ReceiveTradeDrop(FromOffer, trade, own, false);

        ImGui.TableNextColumn();
        var offerHeight = narrow ? height * .31f : (height - ImGui.GetStyle().ItemSpacing.Y) / 2;
        ImGui.BeginChild("MyOffer", new(0, offerHeight), true);
        ImGui.TextUnformatted($"Your offer · {tradeDraft!.Items.Count} / {lineLimit}");
        FieldLabel("Amount of " + community!.Currency.Name);
        ImGui.SetNextItemWidth(Math.Min(200, ImGui.GetContentRegionAvail().X));
        ImGui.InputText("##TradeCurrency", ref tradeDraft.CurrencyText, 64,
            editable ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.ReadOnly);
        tradeDraft.CurrencyFocused = editable && ImGui.IsItemActive();
        if (editable && ImGui.IsItemDeactivatedAfterEdit() && tradeDraft.CommitCurrency()) tradeConflictRetries = 0;
        if (tradeDraft.Error != null) Wrapped(tradeDraft.Error);
        ImGui.TextDisabled(tradeDraft.Dirty ? "Updating offer…" : own.Confirmed ? "Accepted" : "Offer saved");
        columns = TradeColumns(cell);
        var lines = tradeDraft.Items.ToArray();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var item = tradeInventory.FirstOrDefault(x => x.Id == line.Key) ?? draftTradeHoldings.GetValueOrDefault(line.Key);
            var saved = own.Items.FirstOrDefault(x => x.HoldingId == line.Key);
            StartTradeCell(i, columns);
            DrawTradeCell(line.Key, item?.Name ?? saved?.Name ?? "Item", item?.TypeId ?? saved?.TypeId ?? "",
                line.Value, editable ? FromOffer : null, trade.Id,
                editable ? () => RequestTradeQuantity(trade, own, line.Key, false) : null);
        }
        DrawEmptyTradeCells(lines.Length, lineLimit, columns, cell);
        ImGui.EndChild();
        if (editable) ReceiveTradeDrop(FromInventory, trade, own, true);

        ImGui.BeginChild("OtherOffer", new(0, offerHeight), true);
        ImGui.TextUnformatted($"{other.Character.DisplayName} offers · {other.Items.Length} / {lineLimit}");
        ImGui.TextUnformatted(other.CurrencyAmount + " " + community.Currency.Name);
        columns = TradeColumns(cell);
        for (var i = 0; i < other.Items.Length; i++)
        {
            var item = other.Items[i];
            StartTradeCell(i, columns);
            DrawTradeCell(item.HoldingId, item.Name, item.TypeId, item.Quantity, null, trade.Id, null, item.Description);
        }
        DrawEmptyTradeCells(other.Items.Length, lineLimit, columns, cell);
        ImGui.EndChild();
        ImGui.EndTable();
    }

    private static int TradeColumns(float cell) => Math.Clamp((int)((ImGui.GetContentRegionAvail().X + ImGui.GetStyle().ItemSpacing.X)
        / (cell + ImGui.GetStyle().ItemSpacing.X)), 1, 5);

    private static void StartTradeCell(int index, int columns)
    {
        if (index % columns != 0) ImGui.SameLine();
    }

    private static void DrawEmptyTradeCells(int filled, int limit, int columns, float cell)
    {
        // Keep large server limits from flooding the screen with unused cells.
        var count = Math.Min(limit, Math.Max(20, filled));
        for (var i = filled; i < count; i++)
        {
            StartTradeCell(i, columns);
            ImGui.PushID(i);
            ImGui.BeginDisabled();
            ImGui.Button("##EmptyTradeSlot", new Vector2(cell));
            ImGui.EndDisabled();
            ImGui.PopID();
        }
    }

    private static void DrawTradeCell(Guid holding, string name, string type, int quantity,
        string? source, Guid tradeId, Action? onMenu, string? description = null)
    {
        ImGui.PushID(holding.ToString());
        var size = new Vector2(ImGui.GetFrameHeight() * 1.65f);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button(ItemTypeIcon(type).ToIconString() + "###TradeItem", size);
        ImGui.PopFont();
        var corner = ImGui.GetItemRectMax();
        var count = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ImGui.GetWindowDrawList().AddText(corner - ImGui.CalcTextSize(count) - new Vector2(3, 2),
            ImGui.GetColorU32(quantity == 0 ? ImGuiCol.TextDisabled : ImGuiCol.Text), count);
        var hovered = ImGui.IsItemHovered();
        if (source != null && ImGui.BeginDragDropSource())
        {
            var payload = new TradeDrag(tradeId, holding);
            ImGui.SetDragDropPayload(source, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref payload, 1)));
            ImGui.TextUnformatted(name);
            ImGui.EndDragDropSource();
        }
        if (onMenu != null && ImGui.BeginPopupContextItem("TradeItemActions"))
        {
            if (ImGui.MenuItem(source == FromInventory ? "Add to offer" : "Remove from offer")) onMenu();
            ImGui.EndPopup();
        }
        if (hovered)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(name);
            ImGui.TextUnformatted($"{TypeName(type)} · Quantity {quantity}");
            if (!string.IsNullOrEmpty(description))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetFontSize() * 26);
                ImGui.TextUnformatted(description);
                ImGui.PopTextWrapPos();
            }
            ImGui.EndTooltip();
        }
        ImGui.PopID();
    }

    private readonly record struct TradeDrag(Guid Trade, Guid Holding);

    private unsafe void ReceiveTradeDrop(string type, Trade trade, TradeOffer own, bool add)
    {
        if (!ImGui.BeginDragDropTarget()) return;
        var payload = ImGui.AcceptDragDropPayload(type);
        if (!payload.IsNull && payload.IsDelivery() && payload.DataSize == sizeof(TradeDrag))
        {
            var item = *(TradeDrag*)payload.Data;
            if (item.Trade == trade.Id) RequestTradeQuantity(trade, own, item.Holding, add);
        }
        ImGui.EndDragDropTarget();
    }

    private int AvailableForTrade(TradeOffer own, Holding item)
    {
        var saved = own.Items.FirstOrDefault(x => x.HoldingId == item.Id)?.Quantity ?? 0;
        var draft = tradeDraft!.Items.GetValueOrDefault(item.Id);
        return (int)Math.Clamp((long)item.Available + saved - draft, 0, int.MaxValue - draft);
    }

    private int MoveLimit(TradeOffer own, Guid holding, bool add)
    {
        if (!add) return tradeDraft!.Items.GetValueOrDefault(holding);
        var item = tradeInventory.FirstOrDefault(x => x.Id == holding);
        return item == null ? 0 : AvailableForTrade(own, item);
    }

    private void RequestTradeQuantity(Trade trade, TradeOffer own, Guid holding, bool add)
    {
        if (WriteBlocked || tradeCancelRequested || tradeReadyRequested || Reviewing(trade) || trade.State != "open") return;
        var max = MoveLimit(own, holding, add);
        if (max == 0) return;
        if (add && !tradeDraft!.Items.ContainsKey(holding) && tradeDraft.Items.Count >= (serviceLimits?.MaxTradeLinesPerSide ?? 20))
        {
            tradeFeedback = "The offer has reached its item-line limit.";
            return;
        }
        if (max == 1)
        {
            MoveTradeItem(trade, holding, add, 1);
            return;
        }
        tradeQuantityMove = (trade.Id, holding, add);
        tradeMoveQuantity = 1;
        openTradeQuantity = true;
    }

    private void DrawTradeQuantity(Trade trade, TradeOffer own)
    {
        if (openTradeQuantity) { ImGui.OpenPopup(QuantityPopup); openTradeQuantity = false; }
        if (!ImGui.BeginPopupModal(QuantityPopup, ImGuiWindowFlags.AlwaysAutoResize)) return;
        if (tradeQuantityMove is not { } move || move.TradeId != trade.Id || trade.State != "open"
            || Reviewing(trade) || tradeCancelRequested || tradeReadyRequested)
        {
            tradeQuantityMove = null;
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }
        var max = MoveLimit(own, move.HoldingId, move.Add);
        var name = tradeInventory.FirstOrDefault(x => x.Id == move.HoldingId)?.Name
            ?? own.Items.FirstOrDefault(x => x.HoldingId == move.HoldingId)?.Name
            ?? draftTradeHoldings.GetValueOrDefault(move.HoldingId)?.Name ?? "Item";
        Wrapped(name);
        ImGui.TextDisabled($"Maximum: {max}");
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
        ImGui.InputInt("Quantity", ref tradeMoveQuantity);
        ImGui.BeginDisabled(max < 1 || tradeMoveQuantity < 1 || tradeMoveQuantity > max || WriteBlocked);
        if (ImGui.Button(move.Add ? "Add to offer" : "Remove from offer"))
        {
            MoveTradeItem(trade, move.HoldingId, move.Add, tradeMoveQuantity);
            tradeQuantityMove = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) { tradeQuantityMove = null; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }

    private void MoveTradeItem(Trade trade, Guid holding, bool add, int quantity)
    {
        if (add && tradeInventory.FirstOrDefault(item => item.Id == holding) is { } item)
            draftTradeHoldings[holding] = item;
        var total = tradeDraft!.Items.GetValueOrDefault(holding) + (add ? quantity : -quantity);
        ChangeOfferItem(trade, holding, total);
        if (total == 0) draftTradeHoldings.Remove(holding);
    }

    private void CloseTradeQuantity()
    {
        tradeQuantityMove = null;
        openTradeQuantity = false;
        if (ImGui.BeginPopupModal(QuantityPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private static string TradeInventoryPath(Guid id, string query, string cursor) =>
        $"api/v2/seasons/{id}/inventory{query}{(query.Length == 0 ? "?" : "&")}cursor={Uri.EscapeDataString(cursor)}";

    private void LoadMoreTradeInventory(Trade trade)
    {
        if (Busy || session == null || tradeInventoryCursor is not { } cursor) return;
        var uri = origin!;
        var token = session.AccessToken;
        var query = tradeInventoryQuery;
        Start(async ct =>
        {
            var page = await client.GetAsync<HoldingPage>(uri, TradeInventoryPath(trade.CommunityId, query, cursor), token, ct);
            return () =>
            {
                if (SelectedTrade?.Id != trade.Id || query != tradeInventoryQuery || cursor != tradeInventoryCursor) return;
                tradeInventory = tradeInventory.Concat(page.Items).DistinctBy(x => x.Id).ToArray();
                tradeInventoryCursor = page.NextCursor;
            };
        }, "Reading trade inventory…");
    }
}
