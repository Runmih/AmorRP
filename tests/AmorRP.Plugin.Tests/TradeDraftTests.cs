using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;
using Xunit;
namespace AmorRP.Plugin.Tests;

public sealed class TradeDraftTests
{
    private static readonly Guid Actor = Guid.NewGuid(), Other = Guid.NewGuid(), Item = Guid.NewGuid();
    private static Character Person(Guid id) => new(id, "Test character", "35", "Zalera", DateTimeOffset.UtcNow, "verified");
    private static Trade Trade(int revision, int ownQuantity = 0, string otherCurrency = "0") => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "open", revision,
        DateTimeOffset.UtcNow.AddMinutes(10), new(Person(Actor), "0", ownQuantity == 0 ? [] : [new(Item, ownQuantity, "potion", "Potion", null, null, null)], false), new(Person(Other), otherCurrency, [], false), null, null);
    [Theory]
    [InlineData("04", "4", true)]
    [InlineData("0000", "0", true)]
    [InlineData("009223372036854775807", "9223372036854775807", true)]
    [InlineData("9223372036854775808", "0", false)]
    [InlineData("-4", "0", false)]
    [InlineData("4.1", "0", false)]
    [InlineData("４", "0", false)]
    public void Currency_normalizes_leading_zeroes_without_accepting_overflow_or_non_integer_input(string input, string expected, bool valid)
    { Assert.Equal(valid, WholeUnits.TryNormalize(input, out var actual, out _)); if (valid) Assert.Equal(expected, actual); }
    [Fact]
    public void Peer_edit_rebases_onto_fresh_revision_without_losing_my_changes()
    {
        var original = Trade(2); var draft = new TradeDraft(original, Actor); draft.ChangeItem(Item, 2); draft.CurrencyText = "04"; Assert.True(draft.CommitCurrency());
        var peer = original with { Revision = 3, Recipient = original.Recipient with { CurrencyAmount = "7" } };
        draft.Observe(peer, Actor); Assert.True(draft.CanRebase(peer, Actor)); var request = draft.Request(peer);
        Assert.Equal(3, request.ExpectedTradeRevision); Assert.Equal("4", request.CurrencyAmount); Assert.Equal(2, request.Items.Single().Quantity);
    }
    [Fact]
    public void Response_to_prior_submission_preserves_edits_made_while_it_was_in_flight()
    {
        var original = Trade(2); var draft = new TradeDraft(original, Actor); draft.ChangeItem(Item, 2); var sentGeneration = draft.Generation;
        draft.ChangeItem(Item, 3);
        var saved = original with { Revision = 3, Initiator = original.Initiator with { Items = [new(Item, 2, "potion", "Potion", null, null, null)] } };
        draft.Acknowledge(saved, Actor, sentGeneration); Assert.True(draft.Dirty); Assert.Equal(3, draft.Items[Item]); Assert.True(draft.CanRebase(saved, Actor));
        var next = draft.Request(saved); Assert.Equal(3, next.Items.Single().Quantity);
    }
    [Fact]
    public void Focused_currency_text_survives_sync_and_keeps_the_original_own_side_baseline()
    {
        var original = Trade(2); var draft = new TradeDraft(original, Actor) { CurrencyFocused = true, CurrencyText = "04" };
        var elsewhere = original with { Revision = 3, Initiator = original.Initiator with { CurrencyAmount = "5" } };
        draft.Observe(elsewhere, Actor); Assert.Equal("04", draft.CurrencyText); Assert.False(draft.CanRebase(elsewhere, Actor));
        draft.CurrencyFocused = false; Assert.True(draft.CommitCurrency()); Assert.Equal("4", draft.Currency); Assert.True(draft.Dirty);
    }
    [Fact]
    public void Own_offer_changed_on_another_client_cannot_be_overwritten_by_a_blind_rebase()
    {
        var original = Trade(2); var draft = new TradeDraft(original, Actor); draft.ChangeItem(Item, 2);
        var elsewhere = original with { Revision = 3, Initiator = original.Initiator with { CurrencyAmount = "5" } };
        Assert.False(draft.CanRebase(elsewhere, Actor)); draft.AdoptExternal(elsewhere, Actor); Assert.False(draft.Dirty); Assert.Equal("5", draft.CurrencyText); Assert.NotNull(draft.Error);
    }
}
