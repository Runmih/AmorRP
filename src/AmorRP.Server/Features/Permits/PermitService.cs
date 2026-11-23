using System.Globalization;
using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Permits;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Billing;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Features.Inventory;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Features.Shop;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Permits;

public sealed class PermitService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons, CommandStore commands,
    DebtSettlement settlement, InventoryDelivery delivery, TimeProvider time)
{
    public const string ManageListings = "permits.list.manage";

    public async Task<IResult> ReadAsync(HttpContext h, Guid seasonId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, _) = await seasons.RequireAsync(seasonId, actor, ct);
        var now = time.GetUtcNow();
        var listings = await ListingsAsync(season, actor, ct);
        var offers = await db.PermitOffers.AsNoTracking().Where(x => x.CommunityId == seasonId
            && x.Private && (x.CharacterId == actor || x.BuyerId == actor) && x.Status == "pending" && x.ExpiresAt > now)
            .OrderBy(x => x.ExpiresAt).ToArrayAsync(ct);
        var visible = new List<PermitOffer>();
        foreach (var offer in offers)
        {
            var participant = await seasons.TargetAsync(seasonId, offer.CharacterId, true, ct);
            var group = await db.Groups.AsNoTracking().SingleAsync(x => x.CommunityId == seasonId && x.Id == offer.GroupId, ct);
            if (!group.Deleted) visible.Add(await ViewOfferAsync(season, participant, group, offer, actor, ct));
        }
        var permits = await (from p in db.OwnershipPermits.AsNoTracking()
            join g in db.Groups on new { p.CommunityId, Id = p.GroupId } equals new { g.CommunityId, g.Id }
            join c in db.Characters on p.CharacterId equals c.Id
            where p.CommunityId == seasonId && p.ReleasedAt == null && (p.CharacterId == actor || g.OwnerCharacterId == actor)
            orderby p.RegisteredAt select new OwnershipPermit(p.Id, p.CharacterId, c.DisplayName, g.Id, g.Name, p.RegisteredAt, p.ReleasedAt)).ToArrayAsync(ct);
        return Results.Json(new PermitShop(new(Units(season.PermitPrice), season.PermitEnabled), listings, visible.ToArray(), permits));
    }

    public Task<IResult> PolicyAsync(HttpContext h, Guid season, PermitPolicy body, CancellationToken ct) =>
        RunAsync(h, season, body, "permit.policy", async (model, actor, op) =>
        {
            SeasonAccess.Owner(model, actor.CharacterId);
            ApiFault.Require(WholeUnits.TryNormalize(body.BasePrice, out _, out var price) && price > 0,
                422, "invalid_price", "Enter a positive whole-unit base price.");
            model.PermitPrice = price;
            model.PermitEnabled = body.Enabled;
            return new PermitPolicy(Units(price), model.PermitEnabled);
        }, ct);

    public Task<IResult> NominateAsync(HttpContext h, Guid season, PermitListingEdit body, CancellationToken ct) =>
        RunAsync(h, season, body, "permit.nominate", async (model, actor, op) =>
        {
            SeasonAccess.Action(model, actor, ManageListings);
            RequireEnabled(model);
            await seasons.TargetAsync(season, body.CharacterId, true, ct);
            await RequireAvailableAsync(season, body.CharacterId, ct);
            var listing = await FindListingAsync(season, body.CharacterId, ct);
            ApiFault.Require(listing == null || listing.Status is "completed" or "withdrawn", 409, "listing_exists", "This character already has a listing or request.");
            if (listing == null)
            {
                listing = new PermitListingRow { CommunityId = season, CharacterId = body.CharacterId };
                db.PermitListings.Add(listing);
            }
            else
            {
                listing.Status = "pending";
                listing.Version++;
            }
            op.SubjectCharacterId = body.CharacterId;
            return new PermitListing(listing.CharacterId, await NameAsync(listing.CharacterId, ct), listing.Status,
                Units(await ListedTotalAsync(model, listing.CharacterId, ct)), ListingEtag(listing));
        }, ct);

    public Task<IResult> ListingConsentAsync(HttpContext h, Guid season, PermitListingConsent body, CancellationToken ct) =>
        RunAsync(h, season, body, "permit.listing_consent", async (model, actor, op) =>
        {
            var listing = await FindListingAsync(season, actor.CharacterId, ct);
            ApiFault.Require(listing != null && listing.Status is "pending" or "listed", 404, "not_found", "Listing unavailable.");
            CommandStore.Match(h, ListingEtag(listing!));
            if (body.Publish)
            {
                RequireEnabled(model);
                ApiFault.Require(body.ExpectedTotal == Units(await ListedTotalAsync(model, actor.CharacterId, ct)),
                    409, "permit_quote_changed", "The listing price changed. Review the updated listing price.");
                await RequireAvailableAsync(season, actor.CharacterId, ct);
                ApiFault.Require(listing!.Status == "pending", 409, "listing_exists", "This listing is already public.");
            }
            listing!.Status = body.Publish ? "listed" : "withdrawn";
            listing.Version++;
            op.SubjectCharacterId = actor.CharacterId;
            return new PermitListing(actor.CharacterId, await NameAsync(actor.CharacterId, ct), listing.Status,
                Units(await ListedTotalAsync(model, actor.CharacterId, ct)), ListingEtag(listing));
        }, ct);

    public Task<IResult> BuyAsync(HttpContext h, Guid season, PermitPurchase body, CancellationToken ct) =>
        RunAsync(h, season, body, "permit.purchase", async (model, actor, op) =>
        {
            RequireEnabled(model);
            ApiFault.Require(body.CharacterId != actor.CharacterId, 422, "invalid_target", "Choose another character.");
            var group = await BuyerGroupAsync(season, body.GroupId, actor.CharacterId, ct);
            var listing = await FindListingAsync(season, body.CharacterId, ct);
            ApiFault.Require(listing?.Status == "listed", 409, "listing_unavailable", "This listing is no longer available.");
            CommandStore.Match(h, ListingEtag(listing!));
            await RequireAvailableAsync(season, body.CharacterId, ct);
            var participant = await seasons.TargetAsync(season, body.CharacterId, true, ct);

            // Each registered permit has a purchase or offer record for provenance.
            var purchase = new PermitOfferRow
            {
                CommunityId = season, CharacterId = body.CharacterId, BuyerId = actor.CharacterId,
                GroupId = group.Id, ListingVersion = listing!.Version, ExpiresAt = time.GetUtcNow()
            };
            var quote = await QuoteAsync(model, participant, purchase, ct);
            ApiFault.Require(body.ExpectedTotal == Units(model.PermitPrice + quote.Debt),
                409, "permit_quote_changed", "The price changed. Buy again at the updated price.");
            db.PermitOffers.Add(purchase);
            return await SettlePermitAsync(model, actor, group, participant, purchase, quote, op, ct);
        }, ct);

    public Task<IResult> OfferAsync(HttpContext h, Guid season, PermitOfferCreate body, CancellationToken ct) =>
        RunAsync(h, season, body, "permit.offer", async (model, actor, op) =>
        {
            RequireEnabled(model);
            ApiFault.Require(body.CharacterId != actor.CharacterId, 422, "invalid_target", "Choose another character.");
            var group = await BuyerGroupAsync(season, body.GroupId, actor.CharacterId, ct);
            var participant = await seasons.TargetAsync(season, body.CharacterId, true, ct);
            await RequireAvailableAsync(season, body.CharacterId, ct);
            ApiFault.Require(body.Private, 422, "private_offer_required", "Use the shop to buy a public listing.");
            var now = time.GetUtcNow();
            var pending = db.PermitOffers.Where(x => x.CommunityId == season && x.Private && x.Status == "pending" && x.ExpiresAt > now);
            ApiFault.Require(!await pending.AnyAsync(x => x.GroupId == body.GroupId && x.CharacterId == body.CharacterId, ct),
                409, "offer_exists", "This group already has a pending offer for that character.");
            ApiFault.Require(await pending.CountAsync(x => x.BuyerId == actor.CharacterId, ct) < 20
                && await pending.CountAsync(x => x.CharacterId == body.CharacterId, ct) < 20,
                409, "offers_full", "Too many offers are pending.");
            var offer = new PermitOfferRow { CommunityId = season, CharacterId = body.CharacterId, GroupId = body.GroupId,
                BuyerId = actor.CharacterId, Private = true, ExpiresAt = now.AddDays(2) };
            db.PermitOffers.Add(offer);
            op.SubjectCharacterId = body.CharacterId;
            return await ViewOfferAsync(model, participant, group, offer, actor.CharacterId, ct);
        }, ct);

    public Task<IResult> RespondAsync(HttpContext h, Guid season, Guid id, PermitQuoteAction? body, CancellationToken ct) =>
        RunAsync(h, season, body, body == null ? "permit.withdraw" : "permit.accept", async (model, actor, op) =>
        {
            var offer = await FindOfferAsync(season, id, ct);
            ApiFault.Require(actor.CharacterId == offer.CharacterId || body == null && actor.CharacterId == offer.BuyerId,
                403, "offer_participant", "Only the addressed player can accept this offer.");
            var group = await db.Groups.SingleAsync(x => x.CommunityId == season && x.Id == offer.GroupId, ct);
            var participant = await seasons.TargetAsync(season, offer.CharacterId, true, ct);
            if (body == null) offer.Status = "withdrawn";
            else
            {
                RequireEnabled(model);
                await RequireAvailableAsync(season, offer.CharacterId, ct);
                ApiFault.Require(!group.Deleted && group.OwnerCharacterId == offer.BuyerId && group.EnabledFeatures.Contains(GroupCapabilities.Treasury),
                    409, "group_unavailable", "The buying group is no longer eligible.");
                var quote = await QuoteAsync(model, participant, offer, ct);
                MatchQuote(quote.Key, body.Quote);
                offer.AcceptedQuote = quote.Key;
            }
            op.SubjectCharacterId = offer.CharacterId;
            return await ViewOfferAsync(model, participant, group, offer, actor.CharacterId, ct);
        }, ct);

    public Task<IResult> RegisterAsync(HttpContext h, Guid season, Guid id, PermitQuoteAction body, CancellationToken ct) =>
        RunAsync(h, season, body, "permit.register", async (model, actor, op) =>
        {
            RequireEnabled(model);
            var offer = await FindOfferAsync(season, id, ct);
            ApiFault.Require(offer.BuyerId == actor.CharacterId, 403, "offer_participant", "Only the named buyer can register this permit.");
            var group = await BuyerGroupAsync(season, offer.GroupId, actor.CharacterId, ct);
            await RequireAvailableAsync(season, offer.CharacterId, ct);
            var participant = await seasons.TargetAsync(season, offer.CharacterId, true, ct);
            var quote = await QuoteAsync(model, participant, offer, ct);
            MatchQuote(quote.Key, body.Quote);
            ApiFault.Require(offer.AcceptedQuote == quote.Key, 409, "consent_required", "The player must accept this exact offer before registration.");
            return await SettlePermitAsync(model, actor, group, participant, offer, quote, op, ct);
        }, ct);

    private async Task<OwnershipPermit> SettlePermitAsync(CommunityRow model, MembershipRow actor, GroupRow group,
        MembershipRow participant, PermitOfferRow offer, Quote quote, OperationRow op, CancellationToken ct)
    {
        var season = model.Id;
        var total = Total(model.PermitPrice, quote.Debt);
        ShopService.DebitGroup(group, total);
        ShopService.CreditSeason(model, model.PermitPrice);
        await settlement.PayFromGroupAsync(participant, group, quote.Bills, op.Id, ct);
        if (offer.Private)
        {
            await delivery.TransferAvailableAsync(season, participant.CharacterId, actor.CharacterId, quote.Holdings, time.GetUtcNow(), ct);
            if (quote.Currency > 0)
            {
                CurrencyAccounts.Apply(participant, -quote.Currency);
                CurrencyAccounts.Apply(actor, quote.Currency);
                await settlement.CollectAsync(actor, "permit.assets", op.Id, ct);
            }
            model.InventoryVersion++;
        }
        var trust = await db.GroupTrusts.SingleOrDefaultAsync(x => x.CommunityId == season && x.GroupId == group.Id && x.CharacterId == participant.CharacterId, ct);
        if (trust == null)
        {
            trust = new GroupTrustRow { CommunityId = season, GroupId = group.Id, CharacterId = participant.CharacterId };
            db.GroupTrusts.Add(trust);
        }
        trust.Trusted = true; trust.PermitLocked = true; trust.Version++;
        var permit = new OwnershipPermitRow { CommunityId = season, Id = offer.Id, CharacterId = offer.CharacterId,
            GroupId = group.Id, RegisteredAt = time.GetUtcNow() };
        db.OwnershipPermits.Add(permit);
        var listing = await FindListingAsync(season, participant.CharacterId, ct);
        if (listing is { Status: "listed" or "pending" }) { listing.Status = "completed"; listing.Version++; }
        var otherOffers = await db.PermitOffers.Where(x => x.CommunityId == season && x.CharacterId == offer.CharacterId
            && x.Status == "pending" && x.Id != offer.Id).ToArrayAsync(ct);
        foreach (var other in otherOffers) other.Status = "withdrawn";
        offer.Status = "completed";
        op.SubjectCharacterId = offer.CharacterId;
        op.Summary = $"Permit registered for {await NameAsync(offer.CharacterId, ct)} with {group.Name}. Total {total}; debt settled {quote.Debt}.";
        return await ViewPermitAsync(permit, group, ct);
    }

    public Task<IResult> ReleaseAsync(HttpContext h, Guid season, Guid id, CancellationToken ct) =>
        RunAsync(h, season, null, "permit.release", async (model, actor, op) =>
        {
            var permit = await db.OwnershipPermits.SingleOrDefaultAsync(x => x.CommunityId == season && x.Id == id, ct);
            ApiFault.Require(permit != null, 404, "not_found", "Permit unavailable.");
            var group = await db.Groups.SingleAsync(x => x.CommunityId == season && x.Id == permit!.GroupId, ct);
            ApiFault.Require(group.OwnerCharacterId == actor.CharacterId && !group.Deleted, 403, "owner_required", "Only the owning group owner can release this character.");
            await ReleasePermitAsync(db, permit!, time.GetUtcNow(), ct);
            op.SubjectCharacterId = permit!.CharacterId;
            return await ViewPermitAsync(permit, group, ct);
        }, ct);

    internal static async Task ReleasePermitAsync(AmorDbContext db, OwnershipPermitRow permit, DateTimeOffset now, CancellationToken ct)
    {
        if (permit.ReleasedAt.HasValue) return;
        var trust = await db.GroupTrusts.SingleAsync(x => x.CommunityId == permit.CommunityId && x.GroupId == permit.GroupId && x.CharacterId == permit.CharacterId, ct);
        trust.PermitLocked = false;
        trust.Version++;
        permit.ReleasedAt = now;
    }

    private async Task<IResult> RunAsync<T>(HttpContext h, Guid seasonId, object? body, string kind,
        Func<CommunityRow, MembershipRow, OperationRow, Task<T>> action, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var scope = "character:" + actor;
        await CommandStore.LockAsync(db, [scope, "season:" + seasonId], ct);
        var (season, member) = await seasons.RequireAsync(seasonId, actor, ct);
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var op = commands.New(h, scope, body, actor, seasonId, kind);
        var value = await action(season, member, op);
        var result = await commands.SaveAsync(h, op, new CommandResult<T>(op.Id, value), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<PermitListing[]> ListingsAsync(CommunityRow season, Guid actor, CancellationToken ct)
    {
        var rows = await (from l in db.PermitListings.AsNoTracking()
            join c in db.Characters on l.CharacterId equals c.Id
            where l.CommunityId == season.Id && (l.Status == "listed" && (season.PermitEnabled || l.CharacterId == actor) || l.Status == "pending" && l.CharacterId == actor)
            orderby c.DisplayName, c.Id select new { Listing = l, c.DisplayName }).ToArrayAsync(ct);
        var debts = await db.Bills.AsNoTracking().Where(x => x.CommunityId == season.Id && x.Collected < x.Amount)
            .GroupBy(x => x.CharacterId).Select(g => new { CharacterId = g.Key, Debt = g.Sum(x => (decimal)(x.Amount - x.Collected)) }).ToDictionaryAsync(x => x.CharacterId, x => x.Debt, ct);
        return rows.Select(x => new PermitListing(x.Listing.CharacterId, x.DisplayName, x.Listing.Status,
            (season.PermitPrice + debts.GetValueOrDefault(x.Listing.CharacterId)).ToString(CultureInfo.InvariantCulture), ListingEtag(x.Listing))).ToArray();
    }

    private Task<PermitListingRow?> FindListingAsync(Guid season, Guid character, CancellationToken ct) =>
        db.PermitListings.SingleOrDefaultAsync(x => x.CommunityId == season && x.CharacterId == character, ct);

    private async Task<PermitOfferRow> FindOfferAsync(Guid season, Guid id, CancellationToken ct)
    {
        var offer = await db.PermitOffers.SingleOrDefaultAsync(x => x.CommunityId == season && x.Id == id && x.Private, ct);
        ApiFault.Require(offer != null && offer.Status == "pending" && offer.ExpiresAt > time.GetUtcNow(), 404, "not_found", "Offer unavailable.");
        return offer!;
    }

    private async Task<GroupRow> BuyerGroupAsync(Guid season, Guid id, Guid buyer, CancellationToken ct)
    {
        var group = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == season && x.Id == id && !x.Deleted, ct);
        ApiFault.Require(group != null && group.OwnerCharacterId == buyer, 403, "owner_required", "Only a group owner can register a permit for their group.");
        await GroupAuthorization.ActionAsync(db, group!, buyer, GroupCapabilities.TreasuryWithdraw, ct);
        return group!;
    }

    private async Task RequireAvailableAsync(Guid season, Guid character, CancellationToken ct) =>
        ApiFault.Require(!await db.OwnershipPermits.AnyAsync(x => x.CommunityId == season && x.CharacterId == character && x.ReleasedAt == null, ct),
            409, "already_registered", "This character already has an active permit.");

    private static void RequireEnabled(CommunityRow season) =>
        ApiFault.Require(season.PermitEnabled, 403, "permits_disabled", "The season owner has disabled new permits.");

    private static string Units(Int128 value) => value.ToString(CultureInfo.InvariantCulture);
    private static string ListingEtag(PermitListingRow row) => CommandStore.ETag("permit-listing", row.CharacterId, row.Version);
    private static void MatchQuote(string current, string submitted) =>
        ApiFault.Require(current == submitted, 409, "permit_quote_changed", "The price or available assets changed. Review the updated offer.");
    private static long Total(long price, Int128 debt)
    {
        ApiFault.Require(price + debt <= long.MaxValue, 409, "balance_overflow", "The full quote exceeds the currency range.");
        return (long)(price + debt);
    }

    private async Task<Int128> ListedTotalAsync(CommunityRow season, Guid character, CancellationToken ct)
    {
        var amounts = await db.Bills.Where(x => x.CommunityId == season.Id && x.CharacterId == character && x.Collected < x.Amount)
            .Select(x => x.Amount - x.Collected).ToArrayAsync(ct);
        return amounts.Aggregate((Int128)season.PermitPrice, (total, amount) => total + amount);
    }

    private sealed record Quote(string Key, Int128 Debt, BillRow[] Bills, HoldingRow[] Holdings, long Currency, PermitAsset[] Assets);

    private async Task<Quote> QuoteAsync(CommunityRow season, MembershipRow participant, PermitOfferRow offer, CancellationToken ct)
    {
        var bills = await db.Bills.Where(x => x.CommunityId == season.Id && x.CharacterId == participant.CharacterId && x.Amount > x.Collected)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var debt = bills.Aggregate((Int128)0, (total, bill) => total + bill.Amount - bill.Collected);
        var holdings = offer.Private
            ? await db.Holdings.Where(x => x.CommunityId == season.Id && x.CharacterId == participant.CharacterId && x.Quantity > x.Reserved)
                .OrderBy(x => x.Id).ToArrayAsync(ct)
            : [];
        var assets = holdings.Select(x => new PermitAsset(x.Id, x.Name, x.Quantity - x.Reserved, x.Version)).ToArray();
        var currency = offer.Private ? CurrencyAccounts.Available(participant) : 0;
        var terms = new { offer.Id, offer.BuyerId, offer.GroupId, season.PermitPrice,
            Bills = bills.Select(x => new { x.Id, x.Amount, x.Collected }), Assets = assets, Currency = currency };
        return new(SecretVault.Hash(JsonSerializer.Serialize(terms, CommandStore.Json)), debt, bills, holdings, currency, assets);
    }

    private async Task<PermitOffer> ViewOfferAsync(CommunityRow season, MembershipRow target, GroupRow group, PermitOfferRow offer, Guid actor, CancellationToken ct)
    {
        var quote = await QuoteAsync(season, target, offer, ct);
        var accepted = offer.AcceptedQuote == quote.Key;
        var maySeeAssets = actor == offer.CharacterId || accepted;
        var maySeeQuote = !offer.Private || maySeeAssets;
        return new(offer.Id, offer.CharacterId, await NameAsync(offer.CharacterId, ct), offer.BuyerId, await NameAsync(offer.BuyerId, ct),
            group.Id, group.Name, offer.Private, offer.Status, offer.ExpiresAt, maySeeQuote ? quote.Key : "", accepted,
            Units(season.PermitPrice), maySeeQuote ? Units(quote.Debt) : "", maySeeQuote ? Units(season.PermitPrice + quote.Debt) : "",
            actor == offer.BuyerId ? Units(group.TreasuryOwned) : null, maySeeAssets ? Units(quote.Currency) : "0", maySeeAssets ? quote.Assets : []);
    }

    private async Task<OwnershipPermit> ViewPermitAsync(OwnershipPermitRow permit, GroupRow group, CancellationToken ct) =>
        new(permit.Id, permit.CharacterId, await NameAsync(permit.CharacterId, ct), group.Id, group.Name, permit.RegisteredAt, permit.ReleasedAt);

    private Task<string> NameAsync(Guid id, CancellationToken ct) =>
        db.Characters.Where(x => x.Id == id).Select(x => x.DisplayName).SingleAsync(ct);
}
