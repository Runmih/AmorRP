using System.Globalization;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Trading;

public sealed partial class TradeService
{
    private async Task<Trade> ViewAsync(TradeRow row, CancellationToken ct)
        => (await ViewsAsync(row.CommunityId, [row], ct))[0];

    // Four set-based lookups for a whole page, independent of trade/line count.
    // Read callers own a repeatable-read snapshot; command callers retain their
    // transaction/community lock and save tracked changes before loading the response.
    private async Task<Trade[]> ViewsAsync(Guid communityId, TradeRow[] rows, CancellationToken ct)
    {
        if (rows.Length == 0) return [];
        var currencyId = await db.Communities.Where(x => x.Id == communityId).Select(x => x.CurrencyId).SingleAsync(ct);
        var tradeIds = rows.Select(x => x.Id).ToArray();
        var lines = await db.TradeLines.AsNoTracking().Where(x => x.CommunityId == communityId && tradeIds.Contains(x.TradeId))
            .OrderBy(x => x.HoldingId).ToArrayAsync(ct);
        var characterIds = rows.SelectMany(x => new[] { x.InitiatorId, x.RecipientId })
            .Concat(lines.Where(x => x.AuthorId.HasValue).Select(x => x.AuthorId!.Value)).Distinct().ToArray();
        var characters = await db.Characters.AsNoTracking().Where(x => characterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, SessionAccess.View, ct);
        var holdingIds = lines.Select(x => x.HoldingId).Distinct().ToArray();
        var holdings = await db.Holdings.AsNoTracking().Where(x => x.CommunityId == communityId && holdingIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DefinitionId, x.DefinitionRevision }).ToDictionaryAsync(x => x.Id, ct);
        var offers = lines.ToLookup(x => (x.TradeId, x.CharacterId));

        TradeOffer Offer(TradeRow row, Guid characterId, long amount, bool confirmed, bool finalConfirmed)
        {
            var items = offers[(row.Id, characterId)].Select(line => new TradeItem(line.HoldingId, line.Quantity,
                line.TypeId, line.Name, line.Description, line.UseMessage,
                line.AuthorId.HasValue ? characters[line.AuthorId.Value] : null,
                holdings[line.HoldingId].DefinitionId, holdings[line.HoldingId].DefinitionRevision)).ToArray();
            return new(characters[characterId], amount.ToString(CultureInfo.InvariantCulture), items, confirmed, finalConfirmed);
        }
        return rows.Select(row => new Trade(row.Id, row.CommunityId, currencyId, row.State, row.Revision, row.ExpiresAt,
            Offer(row, row.InitiatorId, row.InitiatorCurrency, row.InitiatorConfirmed, row.InitiatorFinalConfirmed),
            Offer(row, row.RecipientId, row.RecipientCurrency, row.RecipientConfirmed, row.RecipientFinalConfirmed),
            row.EndReason, row.CompletionOperationId)).ToArray();
    }
}
