using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Billing;

// Caller holds the community transaction lock. Money moves only from a character
// into a treasury, including an archived treasury whose owner can collect returns.
// No credit hook invokes another public payment command or recursively pays a character.
public sealed class DebtSettlement(AmorDbContext db, TimeProvider time)
{
    public const int BatchSize = 128;
    public async Task CollectAsync(MembershipRow member, string source, Guid? sourceId, CancellationToken ct)
    {
        member.LastCollectionAttempt = time.GetUtcNow().ToUnixTimeMilliseconds();
        if (member.Owned == member.Reserved) { Pending(member, false); return; }
        var fetched = await db.Bills.Where(x => x.CommunityId == member.CommunityId && x.CharacterId == member.CharacterId && x.Collected < x.Amount)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(BatchSize + 1).ToArrayAsync(ct);
        var bills = fetched.Concat(db.Bills.Local.Where(x => x.CommunityId == member.CommunityId && x.CharacterId == member.CharacterId))
            .DistinctBy(x => x.Id).Where(x => x.Collected < x.Amount).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
        foreach (var bill in bills.Take(BatchSize))
        {
            if (member.Owned == member.Reserved) break;
            var group = await db.Groups.SingleAsync(x => x.CommunityId == bill.CommunityId && x.Id == bill.GroupId, ct);
            var amount = Math.Min(Math.Min(member.Owned - member.Reserved, bill.Amount - bill.Collected), long.MaxValue - group.TreasuryOwned);
            if (amount == 0) { Pending(member, true); return; } // Keep oldest-first priority when the creditor is full.
            db.BillPayments.Add(new BillPaymentRow { CommunityId = bill.CommunityId, BillId = bill.Id, Amount = amount,
                CharacterBefore = member.Owned, CharacterAfter = member.Owned - amount,
                TreasuryBefore = group.TreasuryOwned, TreasuryAfter = group.TreasuryOwned + amount,
                Source = source, SourceId = sourceId, OccurredAt = time.GetUtcNow() });
            member.Owned -= amount; member.BalanceVersion++; group.TreasuryOwned += amount; group.TreasuryVersion++; bill.Collected += amount;
        }
        // A long queue continues durably. Until then every unreserved coin stays
        // unavailable to ordinary debits, offers and deposits, even across restarts.
        Pending(member, member.Owned > member.Reserved && (fetched.Length > BatchSize || bills.Any(x => x.Collected < x.Amount)));
    }
    private static void Pending(MembershipRow member, bool value)
    {
        if (member.CollectionPending == value) return;
        member.CollectionPending = value; member.BalanceVersion++;
    }
}
