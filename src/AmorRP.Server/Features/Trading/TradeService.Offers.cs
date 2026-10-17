using System.Globalization;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Trading;

public sealed partial class TradeService
{
    private async Task SetOfferAsync(TradeRow row, Guid actor, TradeOfferWrite body, CancellationToken ct)
    {
        var community = await db.Communities.SingleAsync(x => x.Id == row.CommunityId, ct);
        ApiFault.Require(body.CurrencyId == community.CurrencyId && AmorRP.Contracts.Currency.WholeUnits.TryNormalize(body.CurrencyAmount, out _, out _),
            422, "invalid_quantity", "Specify this community's currency and a nonnegative whole-unit amount.");
        ApiFault.Require(body.Items != null && body.Items.Length <= policy.Value.MaxTradeLinesPerSide
            && body.Items.All(x => x.Quantity > 0) && body.Items.Select(x => x.HoldingId).Distinct().Count() == body.Items.Length,
            422, "invalid_offer", "Offer contains too many, repeated or invalid item lines.");
        AmorRP.Contracts.Currency.WholeUnits.TryNormalize(body.CurrencyAmount, out _, out var amount);
        var member = await db.Memberships.SingleAsync(x => x.CommunityId == row.CommunityId && x.CharacterId == actor, ct);
        var oldAmount = actor == row.InitiatorId ? row.InitiatorCurrency : row.RecipientCurrency;
        ApiFault.Require(amount <= member.Owned - member.Reserved + oldAmount, 409, "insufficient_available_quantity", "Insufficient available currency.");
        member.Reserved = member.Reserved - oldAmount + amount; member.BalanceVersion++;
        if (actor == row.InitiatorId) row.InitiatorCurrency = amount; else row.RecipientCurrency = amount;
        var previous = await db.TradeLines.Where(x => x.CommunityId == row.CommunityId && x.TradeId == row.Id && x.CharacterId == actor).ToArrayAsync(ct);
        foreach (var old in previous)
        {
            var holding = await db.Holdings.SingleAsync(x => x.CommunityId == row.CommunityId && x.Id == old.HoldingId, ct);
            holding.Reserved -= old.Quantity; holding.Version++;
            if (!body.Items!.Any(x => x.HoldingId == old.HoldingId)) db.TradeLines.Remove(old);
        }
        foreach (var line in body.Items!)
        {
            var holding = await db.Holdings.SingleOrDefaultAsync(x => x.CommunityId == row.CommunityId && x.Id == line.HoldingId && x.CharacterId == actor && x.Quantity > 0, ct);
            ApiFault.Require(holding != null, 404, "not_found", "An offered item is no longer owned in this community.");
            ApiFault.Require(line.Quantity <= holding!.Quantity - holding.Reserved, 409, "insufficient_available_quantity", "Insufficient unreserved item quantity.");
            holding.Reserved += line.Quantity; holding.Version++;
            var snapshot = previous.SingleOrDefault(x => x.HoldingId == line.HoldingId);
            if (snapshot == null) { snapshot = new TradeLineRow { CommunityId = row.CommunityId, TradeId = row.Id, CharacterId = actor, HoldingId = holding.Id }; db.TradeLines.Add(snapshot); }
            snapshot.Quantity = line.Quantity; snapshot.TypeId = holding.TypeId; snapshot.Name = holding.Name;
            if (holding.TypeId == "potion")
            {
                var recipe = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == row.CommunityId && x.DefinitionId == holding.DefinitionId && x.Revision == holding.DefinitionRevision, ct);
                snapshot.Description = recipe.Description; snapshot.UseMessage = recipe.UseMessage;
            }
            else snapshot.AuthorId = (await db.Letters.SingleAsync(x => x.CommunityId == row.CommunityId && x.Id == holding.LetterId, ct)).AuthorCharacterId;
        }
        row.Revision++; row.InitiatorConfirmed = false; row.RecipientConfirmed = false; community.InventoryVersion++;
    }
    private async Task CompleteAsync(TradeRow row, CancellationToken ct)
    {
        var members = await db.Memberships.Where(x => x.CommunityId == row.CommunityId && (x.CharacterId == row.InitiatorId || x.CharacterId == row.RecipientId)).ToArrayAsync(ct);
        var a = members.Single(x => x.CharacterId == row.InitiatorId); var b = members.Single(x => x.CharacterId == row.RecipientId);
        long nextA, nextB;
        try { nextA = checked(a.Owned - row.InitiatorCurrency + row.RecipientCurrency); nextB = checked(b.Owned - row.RecipientCurrency + row.InitiatorCurrency); }
        catch (OverflowException) { throw new ApiFault(409, "balance_overflow", "A resulting balance would exceed the supported range. Change the offer."); }
        var lines = await db.TradeLines.Where(x => x.CommunityId == row.CommunityId && x.TradeId == row.Id).ToArrayAsync(ct);
        var holdings = (await db.Holdings.Where(x => x.CommunityId == row.CommunityId && (x.CharacterId == row.InitiatorId || x.CharacterId == row.RecipientId)).ToArrayAsync(ct)).ToList();
        await reservations.ReleaseAsync(row, ct);
        a.Owned = nextA; b.Owned = nextB; a.BalanceVersion++; b.BalanceVersion++;
        // Net outgoing first: full-inventory swaps and near-limit stack exchanges remain possible.
        foreach (var line in lines.Where(x => x.TypeId == "potion")) { var source = holdings.Single(x => x.Id == line.HoldingId); source.Quantity -= line.Quantity; source.Version++; }
        foreach (var line in lines)
        {
            var source = holdings.Single(x => x.Id == line.HoldingId);
            var recipient = line.CharacterId == row.InitiatorId ? row.RecipientId : row.InitiatorId;
            if (source.TypeId != "potion")
            {
                source.CharacterId = recipient; source.Version++;
                (await db.Letters.SingleAsync(x => x.CommunityId == row.CommunityId && x.Id == source.LetterId, ct)).FirstTradedAt ??= time.GetUtcNow();
            }
            else
            {
                var destination = holdings.SingleOrDefault(x => x.CharacterId == recipient && x.DefinitionId == source.DefinitionId && x.DefinitionRevision == source.DefinitionRevision);
                if (destination == null)
                {
                    destination = new HoldingRow { CommunityId = row.CommunityId, CharacterId = recipient, TypeId = source.TypeId, Name = source.Name, CategoryId = source.CategoryId, DefinitionId = source.DefinitionId, DefinitionRevision = source.DefinitionRevision };
                    db.Holdings.Add(destination); holdings.Add(destination);
                }
                try { destination.Quantity = checked(destination.Quantity + line.Quantity); }
                catch (OverflowException) { throw new ApiFault(409, "inventory_overflow", "A resulting stack would exceed the supported range."); }
                destination.Version++;
            }
        }
        foreach (var id in new[] { row.InitiatorId, row.RecipientId })
            ApiFault.Require(holdings.Count(x => x.CharacterId == id && x.Quantity > 0) <= policy.Value.MaxHoldingsPerCharacterCommunity,
                409, "inventory_full", "A participant has insufficient inventory space. Change the offer.");
        row.State = "completed"; row.EndReason = "Both participants completed final confirmation of this revision.";
    }
    private async Task<string> CompletionSummaryAsync(TradeRow row, CancellationToken ct)
    {
        var lines = await db.TradeLines.Where(x => x.CommunityId == row.CommunityId && x.TradeId == row.Id).ToArrayAsync(ct);
        string Items(Guid id) => string.Join(", ", lines.Where(x => x.CharacterId == id).Select(x => $"{x.Quantity} × {x.Name}"));
        return $"Trade completed: initiator sent {row.InitiatorCurrency.ToString(CultureInfo.InvariantCulture)} currency [{Items(row.InitiatorId)}]; recipient sent {row.RecipientCurrency.ToString(CultureInfo.InvariantCulture)} currency [{Items(row.RecipientId)}].";
    }
}
