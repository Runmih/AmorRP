using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Inventory;

public sealed class InventoryDelivery(AmorDbContext db, IOptions<ServicePolicyOptions> policy)
{
    // Called under the season command lock so a stack and its capacity check are atomic.
    public async Task<HoldingRow> AddAsync(Guid season, Guid character, DefinitionRow definition,
        DefinitionRevisionRow revision, int quantity, CancellationToken ct)
    {
        var holding = await db.Holdings.SingleOrDefaultAsync(x => x.CommunityId == season && x.CharacterId == character
            && x.DefinitionId == definition.Id && x.DefinitionRevision == revision.Revision, ct)
            ?? new HoldingRow { CommunityId = season, CharacterId = character, TypeId = definition.TypeId, Name = revision.Name,
                CategoryId = revision.CategoryId, DefinitionId = definition.Id, DefinitionRevision = revision.Revision };
        if (holding.Quantity == 0)
            ApiFault.Require(await db.Holdings.CountAsync(x => x.CommunityId == season && x.CharacterId == character && x.Quantity > 0, ct)
                < policy.Value.MaxHoldingsPerCharacterCommunity, 409, "inventory_full", "Inventory holding limit reached.");
        ApiFault.Require(quantity > 0 && (long)holding.Quantity + quantity <= int.MaxValue, 409, "quantity_overflow", "Item stack is full.");
        holding.Quantity += quantity;
        if (db.Entry(holding).State == EntityState.Detached) db.Holdings.Add(holding);
        else holding.Version++;
        return holding;
    }

    public async Task TransferAvailableAsync(Guid season, Guid from, Guid to, HoldingRow[] sources, DateTimeOffset now, CancellationToken ct)
    {
        if (sources.Length == 0) return;
        var inventory = (await db.Holdings.Where(x => x.CommunityId == season && x.CharacterId == to).ToArrayAsync(ct)).ToList();
        foreach (var source in sources)
        {
            var quantity = source.Quantity - source.Reserved;
            ApiFault.Require(source.CommunityId == season && source.CharacterId == from && quantity > 0,
                409, "permit_quote_changed", "The available inventory changed.");
            if (source.LetterId is { } letterId)
            {
                source.CharacterId = to;
                source.Version++;
                (await db.Letters.SingleAsync(x => x.CommunityId == season && x.Id == letterId, ct)).FirstTradedAt ??= now;
                inventory.Add(source);
                continue;
            }
            var target = inventory.FirstOrDefault(x => x.DefinitionId == source.DefinitionId && x.DefinitionRevision == source.DefinitionRevision);
            if (target == null)
            {
                target = new HoldingRow { CommunityId = season, CharacterId = to, TypeId = source.TypeId, Name = source.Name,
                    CategoryId = source.CategoryId, DefinitionId = source.DefinitionId, DefinitionRevision = source.DefinitionRevision };
                db.Holdings.Add(target);
                inventory.Add(target);
            }
            ApiFault.Require((long)target.Quantity + quantity <= int.MaxValue, 409, "quantity_overflow", "The receiving stack is full.");
            source.Quantity -= quantity;
            source.Version++;
            target.Quantity += quantity;
            target.Version++;
        }
        ApiFault.Require(inventory.Count(x => x.Quantity > 0) <= policy.Value.MaxHoldingsPerCharacterCommunity,
            409, "inventory_full", "The buyer's inventory cannot hold all the offered items.");
    }
}
