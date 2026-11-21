using System.Globalization;
using AmorRP.Contracts.Currency;

namespace AmorRP.Server.Features.Billing;

internal readonly record struct BillingSplit(long SeasonTax, long Commission, long GroupTreasury)
{
    private static long Share(long value, int percent) => (long)((Int128)value * percent / 100);

    public static BillingSplit Total(long collected, int taxPercent, int commissionPercent)
    {
        var tax = Share(collected, taxPercent);
        var net = collected - tax;
        var commission = Share(net, commissionPercent);
        return new(tax, commission, net - commission);
    }

    public static BillingSplit Payment(long alreadyCollected, long amount, int taxPercent, int commissionPercent)
    {
        var before = Total(alreadyCollected, taxPercent, commissionPercent);
        var after = Total(alreadyCollected + amount, taxPercent, commissionPercent);
        return new(after.SeasonTax - before.SeasonTax, after.Commission - before.Commission, after.GroupTreasury - before.GroupTreasury);
    }

    public bool Fits(BillingSplit capacity) =>
        SeasonTax <= capacity.SeasonTax && Commission <= capacity.Commission && GroupTreasury <= capacity.GroupTreasury;

    public BillAllocation View() => new(SeasonTax.ToString(CultureInfo.InvariantCulture),
        Commission.ToString(CultureInfo.InvariantCulture), GroupTreasury.ToString(CultureInfo.InvariantCulture));
}
