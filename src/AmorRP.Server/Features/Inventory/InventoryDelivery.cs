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
}
