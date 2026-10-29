using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
namespace AmorRP.Server.Features.Currency;

// The caller holds the community transaction lock. Trading releases its own
// reservation before applying the agreed net delta; all other debits use available funds.
public static class CurrencyAccounts
{
    public static long After(MembershipRow account, long delta)
    {
        long after;
        try { after = checked(account.Owned + delta); }
        catch (OverflowException) { throw new ApiFault(409, "balance_overflow", "The receiving balance would exceed the supported whole-unit range."); }
        ApiFault.Require(after >= account.Reserved, 409, "insufficient_available_quantity", "There are not enough available coins. Coins offered in trades cannot be spent.");
        return after;
    }
    public static void Apply(MembershipRow account, long delta)
    { account.Owned = After(account, delta); account.BalanceVersion++; }
}
