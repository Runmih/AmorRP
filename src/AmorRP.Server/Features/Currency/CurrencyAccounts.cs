using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
namespace AmorRP.Server.Features.Currency;

// The caller holds the community transaction lock. Trading releases its own
// reservation before applying the agreed net delta; all other debits use available funds.
public static class CurrencyAccounts
{
    public static long Available(MembershipRow account) => account.CollectionPending ? 0 : account.Owned - account.Reserved;
    public static long After(MembershipRow account, long delta)
    {
        ApiFault.Require(delta >= 0 || !account.CollectionPending, 409, "repayment_pending", "Coins are being applied to unpaid bills. Try again after repayment finishes.");
        long after;
        try { after = checked(account.Owned + delta); }
        catch (OverflowException) { throw new ApiFault(409, "balance_overflow", "The receiving balance would exceed the supported whole-unit range."); }
        ApiFault.Require(after >= account.Reserved, 409, "insufficient_available_quantity", "There are not enough available coins. Coins offered in trades cannot be spent.");
        return after;
    }
    public static void Apply(MembershipRow account, long delta)
    { account.Owned = After(account, delta); account.BalanceVersion++; if (delta > 0) account.CollectionPending = true; }
    // Only TradeService uses this after releasing its already-authorized reservation.
    public static void CompleteTrade(MembershipRow account, long after)
    {
        ApiFault.Require(after >= account.Reserved, 409, "insufficient_available_quantity", "The trade cannot spend other reserved coins.");
        account.Owned = after; account.BalanceVersion++;
    }
}
