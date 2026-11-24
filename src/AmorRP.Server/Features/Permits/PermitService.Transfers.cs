using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Permits;
using AmorRP.Server.Features.Shop;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Permits;

public sealed partial class PermitService
{
    private static IQueryable<PermitTransferRow> OpenTransfers(AmorDbContext context, Guid season, DateTimeOffset now) =>
        context.PermitTransfers.Where(x => x.CommunityId == season && x.ExpiresAt > now
            && (x.State == "pending" || x.State == "approved"));

    private async Task<PermitTransfer[]> ActiveTransfersAsync(Guid season, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await (from offer in OpenTransfers(db, season, now).AsNoTracking()
            join permit in db.OwnershipPermits on new { offer.CommunityId, Id = offer.PermitId } equals new { permit.CommunityId, permit.Id }
            join seller in db.Groups on new { offer.CommunityId, Id = offer.SellerGroupId } equals new { seller.CommunityId, seller.Id }
            join buyer in db.Groups on new { offer.CommunityId, Id = offer.BuyerGroupId } equals new { buyer.CommunityId, buyer.Id }
            join character in db.Characters on permit.CharacterId equals character.Id
            where permit.ReleasedAt == null && permit.GroupId == seller.Id && !seller.Deleted && !buyer.Deleted
                && seller.OwnerCharacterId == offer.SellerId && buyer.OwnerCharacterId == offer.BuyerId
                && (offer.SellerId == actor || offer.BuyerId == actor || permit.CharacterId == actor)
            orderby offer.ExpiresAt, offer.Id
            select new { Offer = offer, Character = character, Seller = seller.Name, Buyer = buyer.Name }).ToArrayAsync(ct);
        return rows.Select(row => new PermitTransfer(row.Offer.Id, row.Offer.PermitId, row.Character.Id, row.Character.DisplayName,
            row.Offer.SellerId, row.Seller, row.Offer.BuyerId, row.Buyer, Units(row.Offer.Price),
            row.Offer.ExpiresAt, row.Offer.State == "approved")).ToArray();
    }

    public Task<IResult> OfferTransferAsync(HttpContext h, Guid season, Guid permitId, PermitTransferCreate body, CancellationToken ct) =>
        RunAsync(h, season, body, "permit.transfer.offer", async (model, actor, operation) =>
        {
            RequireEnabled(model);
            ApiFault.Require(WholeUnits.TryNormalize(body.Price, out _, out var price) && price >= 0, 422,
                "invalid_price", "Enter a whole-unit price of zero or more.");
            var permit = await db.OwnershipPermits.SingleOrDefaultAsync(x => x.CommunityId == season
                && x.Id == permitId && x.ReleasedAt == null, ct);
            ApiFault.Require(permit != null, 404, "not_found", "Permit unavailable.");
            var seller = await BuyerGroupAsync(season, permit!.GroupId, actor.CharacterId, ct);
            var buyer = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == season && x.Id == body.BuyerGroupId && !x.Deleted, ct);
            ApiFault.Require(buyer != null && buyer.Id != seller.Id && buyer.OwnerCharacterId != seller.OwnerCharacterId
                && buyer.OwnerCharacterId != permit.CharacterId, 422, "invalid_buyer", "Choose another eligible group.");
            await BuyerGroupAsync(season, buyer!.Id, buyer.OwnerCharacterId, ct);
            var now = time.GetUtcNow();
            ApiFault.Require(!await OpenTransfers(db, season, now).AnyAsync(x => x.PermitId == permitId, ct),
                409, "transfer_pending", "Cancel the outstanding offer before making a new one.");
            var row = new PermitTransferRow { CommunityId = season, PermitId = permitId, SellerGroupId = seller.Id, SellerId = actor.CharacterId,
                BuyerGroupId = buyer.Id, BuyerId = buyer.OwnerCharacterId, Price = price, ExpiresAt = now.AddDays(2) };
            db.PermitTransfers.Add(row);
            operation.SubjectCharacterId = permit.CharacterId;
            operation.Summary = "Offered a permit transfer to " + buyer.Name + ".";
            return row.Id;
        }, ct);

    public Task<IResult> DecideTransferAsync(HttpContext h, Guid season, Guid transferId, string decision, CancellationToken ct) =>
        RunAsync(h, season, null, "permit.transfer." + decision, async (model, actor, operation) =>
        {
            var row = await OpenTransfers(db, season, time.GetUtcNow()).SingleOrDefaultAsync(x => x.Id == transferId, ct);
            var permit = row == null ? null : await db.OwnershipPermits.SingleAsync(x => x.CommunityId == season && x.Id == row.PermitId, ct);
            ApiFault.Require(row != null && permit?.ReleasedAt == null && permit?.GroupId == row.SellerGroupId
                && (actor.CharacterId == row.SellerId || actor.CharacterId == row.BuyerId || actor.CharacterId == permit.CharacterId),
                404, "not_found", "Transfer unavailable.");
            operation.SubjectCharacterId = permit!.CharacterId;
            if (decision == "cancel")
            {
                row!.State = "cancelled";
                operation.Summary = "Closed a permit transfer offer.";
                return row.Id;
            }
            RequireEnabled(model);
            var seller = await BuyerGroupAsync(season, row!.SellerGroupId, row.SellerId, ct);
            var buyer = await BuyerGroupAsync(season, row.BuyerGroupId, row.BuyerId, ct);
            if (decision == "approve")
            {
                ApiFault.Require(actor.CharacterId == permit.CharacterId && row.State == "pending", 403,
                    "transfer_consent_required", "Only the registered character can allow the transfer.");
                row.State = "approved";
                operation.Summary = "Approved a permit transfer.";
                return row.Id;
            }

            ApiFault.Require(actor.CharacterId == row.BuyerId && row.State == "approved", 403,
                "transfer_not_approved", "The buyer and the registered character must approve this offer.");
            ApiFault.Require(seller.TreasuryOwned <= long.MaxValue - row.Price, 409, "balance_overflow", "The seller's treasury cannot receive the price.");
            if (row.Price > 0)
            {
                ShopService.DebitGroup(buyer, row.Price);
                seller.TreasuryOwned += row.Price;
                seller.TreasuryVersion++;
            }
            var oldTrust = await db.GroupTrusts.SingleAsync(x => x.CommunityId == season && x.GroupId == seller.Id && x.CharacterId == permit.CharacterId, ct);
            var newTrust = await db.GroupTrusts.SingleOrDefaultAsync(x => x.CommunityId == season && x.GroupId == buyer.Id && x.CharacterId == permit.CharacterId, ct);
            if (newTrust == null)
            {
                newTrust = new GroupTrustRow { CommunityId = season, GroupId = buyer.Id, CharacterId = permit.CharacterId };
                db.GroupTrusts.Add(newTrust);
            }
            oldTrust.PermitLocked = false;
            oldTrust.Version++;
            newTrust.Trusted = true;
            newTrust.PermitLocked = true;
            newTrust.Version++;
            permit.GroupId = buyer.Id;
            row.State = "completed";
            operation.Summary = $"Transferred a permit from {seller.Name} to {buyer.Name} for {Units(row.Price)} coins.";
            return row.Id;
        }, ct);
}
