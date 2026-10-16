using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Trading;
namespace AmorRP.Plugin.Services.Api;

// Local editing only. The server remains authoritative for ownership/reservations.
public sealed class TradeDraft
{
    private TradeOffer baseline;
    public Guid TradeId { get; }
    public string CurrencyText = "0";
    public string Currency { get; private set; }
    public Dictionary<Guid, int> Items { get; } = [];
    public int Generation { get; private set; }
    public bool CurrencyFocused { get; set; }
    public string? Error { get; private set; }
    public TradeDraft(Trade trade, Guid actor)
    {
        TradeId = trade.Id; baseline = Own(trade, actor); Currency = baseline.CurrencyAmount; Adopt(baseline);
    }
    public static TradeOffer Own(Trade t, Guid actor) => t.Initiator.Character.Id == actor ? t.Initiator : t.Recipient;
    public bool Dirty => Currency != baseline.CurrencyAmount || !SameItems(Items, baseline.Items);
    private static bool SameItems(Dictionary<Guid, int> left, TradeItem[] right) => left.Count == right.Length && right.All(x => left.GetValueOrDefault(x.HoldingId) == x.Quantity);
    private static bool Same(TradeOffer a, TradeOffer b) => a.CurrencyAmount == b.CurrencyAmount && SameItems(a.Items.ToDictionary(x => x.HoldingId, x => x.Quantity), b.Items);
    private void Adopt(TradeOffer offer)
    {
        Currency = offer.CurrencyAmount; if (!CurrencyFocused) CurrencyText = Currency;
        Items.Clear(); foreach (var item in offer.Items) Items[item.HoldingId] = item.Quantity;
    }
    public void ChangeItem(Guid id, int quantity)
    {
        if (quantity <= 0) Items.Remove(id); else Items[id] = quantity;
        Generation++; Error = null;
    }
    public bool CommitCurrency()
    {
        if (!WholeUnits.TryNormalize(CurrencyText.Length == 0 ? "0" : CurrencyText, out var normalized, out _))
        { Error = "Enter a nonnegative whole number within the supported currency range."; return false; }
        CurrencyText = normalized; Currency = normalized; Generation++; Error = null; return true;
    }
    public void Observe(Trade trade, Guid actor)
    {
        var own = Own(trade, actor);
        if (!Dirty && !CurrencyFocused) { baseline = own; Adopt(own); }
        // A peer changing their side never overwrites this character's draft.
    }
    public void Pause(string message) => Error = message;
    public bool CanRebase(Trade fresh, Guid actor) => Same(baseline, Own(fresh, actor));
    public TradeOfferWrite Request(Trade fresh) => new(fresh.Revision, fresh.CurrencyId, Currency, Items.Select(x => new TradeLine(x.Key, x.Value)).ToArray());
    public void Acknowledge(Trade trade, Guid actor, int submittedGeneration)
    {
        baseline = Own(trade, actor);
        if (Generation == submittedGeneration) Adopt(baseline);
        Error = null;
    }
    public void AdoptExternal(Trade fresh, Guid actor)
    {
        baseline = Own(fresh, actor); Adopt(baseline); Generation++;
        Error = "Your offer changed on another client. The saved offer is shown; review it before editing again.";
    }
}
