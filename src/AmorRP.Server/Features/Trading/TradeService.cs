using System.Data;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AmorRP.Server.Features.Trading;

public sealed partial class TradeService(AmorDbContext db, SessionAccess sessions, CommunityAccess communities,
    CommandStore commands, TradeReservations reservations, PageCursor pages, TimeProvider time,
    IOptions<ServicePolicyOptions> policy)
{
    public async Task<IResult> ReadAsync(HttpContext h, Guid communityId, Guid? id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        // Participants can recover terminal outcomes after removal, without community/private inventory access.
        ApiFault.Require(await db.Memberships.AnyAsync(x => x.CommunityId == communityId && x.CharacterId == actor, ct), 404, "not_found", "Trades not found.");
        await using (var expiry = await db.Database.BeginTransactionAsync(ct))
        {
            await CommandStore.LockAsync(db, ["community:" + communityId], ct);
            await reservations.ExpireAsync(communityId, ct); await db.SaveChangesAsync(ct);
            await expiry.CommitAsync(ct);
        }
        // Expiry is durable and the community write lock is released before projection.
        // Clear expiry-tracked rows so all returned fields come from the read snapshot.
        db.ChangeTracker.Clear();
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var query = db.Trades.AsNoTracking().Where(x => x.CommunityId == communityId && (x.InitiatorId == actor || x.RecipientId == actor));
        if (id.HasValue)
        {
            var row = await query.SingleOrDefaultAsync(x => x.Id == id, ct);
            ApiFault.Require(row != null, 404, "not_found", "Trade not found.");
            var result = await ViewAsync(row!, ct); await snapshot.CommitAsync(ct); return Results.Json(result);
        }
        var scope = $"trades:{communityId}:{actor}"; var (limit, after) = pages.Read(h, scope);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) < 0);
        var rows = await query.OrderByDescending(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        var items = await ViewsAsync(communityId, rows.Take(limit).ToArray(), ct);
        await snapshot.CommitAsync(ct);
        return Results.Json(new TradePage(items, rows.Length > limit ? pages.Write(rows[limit - 1].Id, scope) : null));
    }
    public async Task<IResult> CommandAsync(HttpContext h, Guid communityId, Guid? id, object body, string kind, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null)
        {
            ApiFault.Require(replay.CommunityId == communityId, 404, "not_found", "Trade not found.");
            return commands.Replay(replay);
        }
        await reservations.ExpireAsync(communityId, ct);
        // Commit expiry even when the submitted command will be rejected as stale.
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await using var commandTx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        db.ChangeTracker.Clear();
        // Another replica may have committed the same key while this request expired trades.
        replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        TradeRow row;
        if (kind == "create")
        {
            var (g, m) = await communities.RequireAsync(communityId, actor, ct);
            var recipient = ((TradeCreate)body).RecipientCharacterId;
            ApiFault.Require(recipient != actor, 422, "invalid_target", "Choose another community member.");
            var other = await communities.TargetAsync(communityId, recipient, true, ct);
            ApiFault.Require(!m.TradingRestricted && !other.TradingRestricted, 403, "trading_restricted", "Trading is restricted for one participant.");
            ApiFault.Require(!await db.TradeSlots.AnyAsync(x => x.CommunityId == communityId && (x.CharacterId == actor || x.CharacterId == recipient), ct),
                409, "trade_busy", "One participant already has a pending trade in this community.");
            row = new TradeRow { CommunityId = communityId, InitiatorId = actor, RecipientId = recipient, ExpiresAt = time.GetUtcNow().AddSeconds(policy.Value.TradeLifetimeSeconds) };
            db.Trades.Add(row); db.TradeSlots.AddRange(new TradeSlotRow { CommunityId = communityId, CharacterId = actor, TradeId = row.Id }, new TradeSlotRow { CommunityId = communityId, CharacterId = recipient, TradeId = row.Id });
        }
        else
        {
            var found = await db.Trades.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == id && (x.InitiatorId == actor || x.RecipientId == actor), ct);
            ApiFault.Require(found != null, 404, "not_found", "Trade not found."); row = found!;
            ApiFault.Require(TradeReservations.Active(row) && row.ExpiresAt > time.GetUtcNow(), 409, "trade_closed", "Trade ended. Read its current outcome.");
            var expected = body is TradeOfferWrite offer ? offer.ExpectedTradeRevision : ((TradeRevision)body).ExpectedTradeRevision;
            // Cancel is not an approval of an offer, so an old revision cannot prevent it.
            // An edit after either first approval ends the entire trade, even if stale.
            if (kind == "cancel" || kind == "offer" && (row.InitiatorConfirmed || row.RecipientConfirmed || row.State == "confirming"))
                await reservations.EndAsync(row, "cancelled", kind == "cancel" ? "Cancelled by a participant." : "Offer edited after confirmation started; trade cancelled.", ct);
            else
            {
                ApiFault.Require(expected == row.Revision, 409, "trade_changed", "Offer changed. Review the current trade before acting.");
                await communities.RequireAsync(communityId, actor, ct);
                var a = await communities.TargetAsync(communityId, row.InitiatorId, true, ct); var b = await communities.TargetAsync(communityId, row.RecipientId, true, ct);
                ApiFault.Require(!a.TradingRestricted && !b.TradingRestricted, 403, "trading_restricted", "Trading is restricted for one participant.");
                if (kind == "accept")
                {
                    ApiFault.Require(actor == row.RecipientId && row.State == "invited", 409, "trade_not_invited", "Only the recipient can accept a pending invitation.");
                    row.State = "open"; row.Revision++;
                }
                else if (kind == "final-confirm")
                {
                    ApiFault.Require(row.State == "confirming" && row.InitiatorConfirmed && row.RecipientConfirmed, 409, "final_review_required", "Both players must accept the offer before final confirmation.");
                    ApiFault.Require(!(actor == row.InitiatorId ? row.InitiatorFinalConfirmed : row.RecipientFinalConfirmed),
                        409, "trade_already_confirmed", "Your final approval is already recorded. Wait for the other participant.");
                    if (actor == row.InitiatorId) row.InitiatorFinalConfirmed = true; else row.RecipientFinalConfirmed = true;
                    if (row.InitiatorFinalConfirmed && row.RecipientFinalConfirmed) await CompleteAsync(row, ct);
                }
                else
                {
                    ApiFault.Require(row.State == "open", 409, "trade_not_open", "Recipient must accept before editing or confirming offers.");
                    if (kind == "offer") await SetOfferAsync(row, actor, (TradeOfferWrite)body, ct);
                    else
                    {
                        ApiFault.Require(row.InitiatorCurrency != 0 || row.RecipientCurrency != 0 || await db.TradeLines.AnyAsync(x => x.CommunityId == communityId && x.TradeId == row.Id, ct), 422, "empty_trade", "Add currency or an item before confirming.");
                        ApiFault.Require(!(actor == row.InitiatorId ? row.InitiatorConfirmed : row.RecipientConfirmed),
                            409, "trade_already_confirmed", "Your offer approval is already recorded. Wait for the other participant.");
                        if (actor == row.InitiatorId) row.InitiatorConfirmed = true; else row.RecipientConfirmed = true;
                        if (row.InitiatorConfirmed && row.RecipientConfirmed) row.State = "confirming";
                    }
                }
            }
        }

        var op = commands.New(h, scope, body, actor, communityId, row.State == "completed" ? "trade.complete" : "trade." + kind);
        op.TradeId = row.Id;
        op.SubjectCharacterId = actor == row.InitiatorId ? row.RecipientId : row.InitiatorId;
        op.Summary = row.State == "completed" ? await CompletionSummaryAsync(row, ct) : $"Trade {kind}; revision {row.Revision}.";
        if (row.State == "completed") row.CompletionOperationId = op.Id;
        // View must include newly tracked lines even before commit.
        await db.SaveChangesAsync(ct);
        var result = await commands.SaveAsync(h, op, new CommandResult<Trade>(op.Id, await ViewAsync(row, ct)), ct);
        await commandTx.CommitAsync(ct); return result;
    }
}
