using System.Globalization;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Shop;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Catalog;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Features.Inventory;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Shop;

public sealed class ShopService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons, CatalogService catalog,
    CommandStore commands, InventoryDelivery delivery, IOptions<ServicePolicyOptions> policy)
{
    private static string Etag(ShopProductRow row) => CommandStore.ETag("shop-product", row.Id, row.Version);
    private static ShopProduct View(ShopProductRow row, DefinitionRevisionRow revision) => new(row.Id, row.DefinitionId, row.DefinitionRevision,
        revision.Name, revision.Description, row.Price.ToString(CultureInfo.InvariantCulture), row.Buyer, row.Stock, row.BuyerLimit, row.Listed, row.Version, Etag(row));

    public async Task<IResult> ListAsync(HttpContext h, Guid seasonId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, _) = await seasons.RequireAsync(seasonId, actor, ct);
        var rows = await (from p in db.ShopProducts.AsNoTracking()
            join d in db.Definitions on new { p.CommunityId, Id = p.DefinitionId } equals new { d.CommunityId, d.Id }
            join r in db.DefinitionRevisions on new { p.CommunityId, p.DefinitionId, Revision = p.DefinitionRevision } equals new { r.CommunityId, r.DefinitionId, r.Revision }
            where p.CommunityId == seasonId && (season.OwnerCharacterId == actor || p.Listed && !d.Retired)
            orderby r.Name, p.Id select new { Product = p, Revision = r }).ToArrayAsync(ct);
        return Results.Json(new ShopProducts(rows.Select(x => View(x.Product, x.Revision)).ToArray()));
    }

    public async Task<IResult> WriteAsync(HttpContext h, Guid seasonId, Guid? productId, ShopProductWrite body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var scope = "character:" + actor;
        await CommandStore.LockAsync(db, [scope, "season:" + seasonId], ct);
        var (season, _) = await seasons.RequireAsync(seasonId, actor, ct);
        SeasonAccess.Owner(season, actor);
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        ApiFault.Require(WholeUnits.TryNormalize(body.Price, out _, out var price) && price > 0
            && body.Buyer is "character" or "group" or "either" && body.Stock is null or >= 0
            && body.BuyerLimit is null or > 0, 422, "invalid_product", "Choose a positive price, buyer type, and valid stock and purchase limits.");
        var product = productId is { } id
            ? await db.ShopProducts.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.Id == id, ct)
            : new ShopProductRow { CommunityId = seasonId };
        ApiFault.Require(product != null, 404, "not_found", "Shop product unavailable.");
        var row = product!;
        if (productId.HasValue)
        {
            CommandStore.Match(h, Etag(row));
            ApiFault.Require(row.DefinitionId == body.DefinitionId, 422, "invalid_product", "Create a new listing to sell a different item.");
            row.Version++;
        }
        else
        {
            ApiFault.Require(await db.ShopProducts.CountAsync(x => x.CommunityId == seasonId, ct) < 100, 409, "shop_full", "The season shop can have up to 100 listings.");
            db.ShopProducts.Add(row);
        }
        var definition = await db.Definitions.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.Id == body.DefinitionId, ct);
        ApiFault.Require(definition != null && CraftingTypes.IsConsumable(definition.TypeId) && (!body.Listed || !definition.Retired),
            422, "invalid_product", "Choose an available Alchemy or Cooking item.");
        var revisionNumber = definition!.Retired ? row.DefinitionRevision : definition.Revision;
        var revision = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == seasonId && x.DefinitionId == body.DefinitionId && x.Revision == revisionNumber, ct);
        if (body.Listed) await catalog.RequireCategoryAsync(seasonId, revision.CategoryId, definition.TypeId, ct);
        row.DefinitionId = body.DefinitionId; row.DefinitionRevision = revision.Revision;
        row.Price = price; row.Buyer = body.Buyer; row.Stock = body.Stock; row.BuyerLimit = body.BuyerLimit; row.Listed = body.Listed;
        var op = commands.New(h, scope, body, actor, seasonId, "shop.product");
        op.Summary = "Updated shop listing: " + revision.Name;
        h.Response.Headers.ETag = Etag(row);
        var result = await commands.SaveAsync(h, op, new CommandResult<ShopProduct>(op.Id, View(row, revision)), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IResult> BuyAsync(HttpContext h, Guid seasonId, ShopBuy body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var scope = "character:" + actor;
        await CommandStore.LockAsync(db, [scope, "season:" + seasonId], ct);
        var (season, member) = await seasons.RequireAsync(seasonId, actor, ct);
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var row = await db.ShopProducts.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.Id == body.ProductId && x.Listed, ct);
        ApiFault.Require(row != null, 404, "not_found", "Shop product unavailable.");
        ApiFault.Require(row!.Version == body.ExpectedVersion, 409, "product_changed", "The listing changed. Review the current price and stock.");
        ApiFault.Require(body.Quantity > 0 && body.Quantity <= policy.Value.MaxCreationQuantity && (row.Stock == null || row.Stock >= body.Quantity)
            && (Int128)row.Price * body.Quantity <= long.MaxValue, 409, "invalid_quantity", "Choose an available quantity.");
        GroupRow? group = null;
        if (body.GroupId is { } id)
        {
            ApiFault.Require(row.Buyer != "character", 403, "buyer_ineligible", "This item is sold to individual buyers.");
            group = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.Id == id && !x.Deleted, ct);
            ApiFault.Require(group != null, 404, "not_found", "Buying group unavailable.");
            await GroupAuthorization.ActionAsync(db, group!, actor, GroupCapabilities.TreasuryWithdraw, ct);
        }
        else ApiFault.Require(row.Buyer != "group", 403, "buyer_ineligible", "This item requires a group treasury purchase.");
        if (row.BuyerLimit is { } limit)
        {
            var past = await db.ShopPurchases.Where(x => x.CommunityId == seasonId && x.ProductId == row.Id
                && x.GroupId == body.GroupId && (body.GroupId != null || x.CharacterId == actor)).SumAsync(x => (long)x.Quantity, ct);
            ApiFault.Require(past <= limit - body.Quantity, 409, "buyer_limit", "This purchase exceeds the buyer limit.");
        }
        var definition = await db.Definitions.SingleAsync(x => x.CommunityId == seasonId && x.Id == row.DefinitionId, ct);
        ApiFault.Require(!definition.Retired, 409, "product_unavailable", "This item has been retired.");
        var revision = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == seasonId && x.DefinitionId == row.DefinitionId && x.Revision == row.DefinitionRevision, ct);
        await catalog.RequireCategoryAsync(seasonId, revision.CategoryId, definition.TypeId, ct);
        var total = row.Price * body.Quantity;
        if (group == null) CurrencyAccounts.Apply(member, -total);
        else DebitGroup(group, total);
        CreditSeason(season, total);
        var holding = await delivery.AddAsync(seasonId, actor, definition, revision, body.Quantity, ct);
        if (row.Stock.HasValue) { row.Stock -= body.Quantity; row.Version++; }
        season.InventoryVersion++;
        var purchase = new ShopPurchaseRow { CommunityId = seasonId, CharacterId = actor, GroupId = group?.Id, ProductId = row.Id, Quantity = body.Quantity };
        db.ShopPurchases.Add(purchase);
        var op = commands.New(h, scope, body, actor, seasonId, "shop.purchase");
        op.SubjectCharacterId = actor;
        op.Summary = $"Shop purchase: {body.Quantity} × {revision.Name} for {total} coins.";
        var result = await commands.SaveAsync(h, op, new CommandResult<ShopPurchase>(op.Id,
            new(purchase.Id, row.Id, revision.Name, body.Quantity, total.ToString(CultureInfo.InvariantCulture), group?.Id, InventoryService.View(holding))), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    internal static void DebitGroup(GroupRow group, long amount)
    {
        ApiFault.Require(group.TreasuryOwned >= amount, 409, "insufficient_available_quantity", "The group treasury cannot cover the cost.");
        group.TreasuryOwned -= amount; group.TreasuryVersion++;
    }

    internal static void CreditSeason(CommunityRow season, long amount)
    {
        ApiFault.Require(season.SeasonTreasuryOwned <= long.MaxValue - amount, 409, "balance_overflow", "The season treasury cannot receive that payment.");
        season.SeasonTreasuryOwned += amount;
    }
}
