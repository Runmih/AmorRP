using AmorRP.Server.Features.Currency;
using AmorRP.Server.Infrastructure.Persistence;
namespace AmorRP.Server.Features.Groups;

// Shared by group deletion and community deletion, inside their community lock
// and command transaction. Future group assets extend this settlement boundary.
public sealed class GroupLifecycle(TreasuryLedger ledger)
{
    public async Task DisbandAsync(GroupRow group, OperationRow operation, CancellationToken ct)
    {
        if (group.Deleted) return;
        await ledger.ReturnToOwnerAsync(group, operation, ct);
        group.Deleted = true;
    }
}
