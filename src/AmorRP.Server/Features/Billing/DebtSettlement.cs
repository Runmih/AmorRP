using AmorRP.Server.Features.Currency;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Billing;

// Every caller holds the season transaction lock. Commission credits are marked
// for the debt worker, which avoids recursive collection across cashier accounts.
public sealed class DebtSettlement(AmorDbContext db, TimeProvider time)
{
    public const int BatchSize = 128;

    internal async Task RequireCapacityAsync(BillRow bill, MembershipRow payer, GroupRow group, long amount, CancellationToken ct)
    {
        if (amount == 0) return;
        var accounts = await GetAccountsAsync(bill, payer, ct);
        var split = BillingSplit.Payment(bill.Collected, amount, bill.TaxPercent, bill.CommissionPercent);
        ApiFault.Require(split.Fits(Capacity(group, payer, accounts)), 409, "balance_overflow",
            "The season treasury, group treasury or cashier cannot receive this payment.");
    }

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
            var accounts = await GetAccountsAsync(bill, member, ct);
            var amount = Math.Min(member.Owned - member.Reserved, bill.Amount - bill.Collected);
            amount = Affordable(bill, amount, Capacity(group, member, accounts));
            if (amount == 0) { Pending(member, true); return; }

            var split = BillingSplit.Payment(bill.Collected, amount, bill.TaxPercent, bill.CommissionPercent);
            db.BillPayments.Add(new BillPaymentRow
            {
                CommunityId = bill.CommunityId, BillId = bill.Id, Amount = amount,
                CharacterBefore = member.Owned, CharacterAfter = member.Owned - amount,
                TreasuryBefore = group.TreasuryOwned, TreasuryAfter = group.TreasuryOwned + split.GroupTreasury,
                SeasonTax = split.SeasonTax, Commission = split.Commission, Source = source, SourceId = sourceId,
                OccurredAt = time.GetUtcNow()
            });
            member.Owned -= amount;
            member.BalanceVersion++;
            if (split.GroupTreasury > 0)
            {
                group.TreasuryOwned += split.GroupTreasury;
                group.TreasuryVersion++;
            }
            if (split.SeasonTax > 0) accounts.Season!.SeasonTreasuryOwned += split.SeasonTax;
            if (split.Commission > 0) CurrencyAccounts.Apply(accounts.Cashier!, split.Commission);
            bill.Collected += amount;

            // A recipient with a full account cannot be skipped in favor of a newer bill.
            if (bill.Collected < bill.Amount) break;
        }
        Pending(member, member.Owned > member.Reserved && (fetched.Length > BatchSize || bills.Any(x => x.Collected < x.Amount)));
    }

    private async Task<(CommunityRow? Season, MembershipRow? Cashier)> GetAccountsAsync(
        BillRow bill, MembershipRow payer, CancellationToken ct)
    {
        var maximum = BillingSplit.Total(bill.Amount, bill.TaxPercent, bill.CommissionPercent);
        var season = maximum.SeasonTax == 0 ? null
            : await db.Seasons.SingleAsync(x => x.Id == bill.CommunityId, ct);
        var cashier = maximum.Commission == 0 ? null : bill.IssuerCharacterId == payer.CharacterId ? payer
            : await db.Memberships.SingleAsync(x => x.CommunityId == bill.CommunityId && x.CharacterId == bill.IssuerCharacterId, ct);
        return (season, cashier);
    }

    private static BillingSplit Capacity(GroupRow group, MembershipRow payer, (CommunityRow? Season, MembershipRow? Cashier) accounts) =>
        new(long.MaxValue - (accounts.Season?.SeasonTreasuryOwned ?? 0),
            ReferenceEquals(accounts.Cashier, payer) ? long.MaxValue : long.MaxValue - (accounts.Cashier?.Owned ?? 0),
            long.MaxValue - group.TreasuryOwned);

    private static long Affordable(BillRow bill, long maximum, BillingSplit capacity)
    {
        bool Fits(long amount) => BillingSplit.Payment(bill.Collected, amount, bill.TaxPercent, bill.CommissionPercent).Fits(capacity);
        if (Fits(maximum)) return maximum;
        // Each account's cumulative allocation is monotonic with tax first.
        long lower = 0, upper = maximum;
        while (upper - lower > 1)
        {
            var middle = lower + (upper - lower) / 2;
            if (Fits(middle)) lower = middle;
            else upper = middle;
        }
        return lower;
    }

    private static void Pending(MembershipRow member, bool value)
    {
        if (member.CollectionPending == value) return;
        member.CollectionPending = value;
        member.BalanceVersion++;
    }
}
