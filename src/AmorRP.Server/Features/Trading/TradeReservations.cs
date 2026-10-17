using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Trading;

// Caller owns a transaction and the community advisory lock. Membership/asset writers
// use the same lock, so cancellation, expiry and exchange cannot race one another.
public sealed class TradeReservations(AmorDbContext db, TimeProvider time)
{
    public static bool Active(TradeRow t) => t.State is "invited" or "open" or "confirming";
    public async Task ReleaseAsync(TradeRow t, CancellationToken ct)
    {
        foreach (var line in await db.TradeLines.Where(x => x.CommunityId == t.CommunityId && x.TradeId == t.Id).ToArrayAsync(ct))
        {
            var holding = await db.Holdings.SingleAsync(x => x.CommunityId == t.CommunityId && x.Id == line.HoldingId, ct);
            holding.Reserved -= line.Quantity; holding.Version++;
        }
        foreach (var id in new[] { t.InitiatorId, t.RecipientId })
        {
            var member = await db.Memberships.SingleAsync(x => x.CommunityId == t.CommunityId && x.CharacterId == id, ct);
            var amount = id == t.InitiatorId ? t.InitiatorCurrency : t.RecipientCurrency;
            if (amount != 0) { member.Reserved -= amount; member.BalanceVersion++; }
        }
        db.TradeSlots.RemoveRange(await db.TradeSlots.Where(x => x.CommunityId == t.CommunityId && x.TradeId == t.Id).ToArrayAsync(ct));
        (await db.Communities.SingleAsync(x => x.Id == t.CommunityId, ct)).InventoryVersion++;
    }
    public async Task EndAsync(TradeRow t, string state, string reason, CancellationToken ct)
    {
        if (!Active(t)) return;
        await ReleaseAsync(t, ct); t.State = state; t.EndReason = reason; t.Revision++;
        t.InitiatorConfirmed = false; t.RecipientConfirmed = false; t.InitiatorFinalConfirmed = false; t.RecipientFinalConfirmed = false;
    }
    public async Task CancelForAsync(Guid community, Guid? member, string reason, CancellationToken ct)
    {
        foreach (var t in await db.Trades.Where(x => x.CommunityId == community && (x.State == "invited" || x.State == "open" || x.State == "confirming")
            && (!member.HasValue || x.InitiatorId == member || x.RecipientId == member)).ToArrayAsync(ct))
            await EndAsync(t, "cancelled", reason, ct);
    }
    public async Task ExpireAsync(Guid community, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var t in await db.Trades.Where(x => x.CommunityId == community && (x.State == "invited" || x.State == "open" || x.State == "confirming") && x.ExpiresAt <= now).ToArrayAsync(ct))
            await EndAsync(t, "expired", "Trade timed out; reservations released.", ct);
    }
}
