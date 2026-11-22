using AmorRP.Server.Features.Currency;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Features.Permits;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Groups;

// Shared by group deletion and community deletion, inside their community lock
// and command transaction. Future group assets extend this settlement boundary.
public sealed class GroupLifecycle(TreasuryLedger ledger, AmorDbContext db, TimeProvider time)
{
    public async Task DisbandAsync(GroupRow group, OperationRow operation, CancellationToken ct)
    {
        if (group.Deleted) return;
        await ledger.ReturnToOwnerAsync(group, operation, ct);
        var permits = await db.OwnershipPermits.Where(x => x.CommunityId == group.CommunityId && x.GroupId == group.Id && x.ReleasedAt == null).ToArrayAsync(ct);
        foreach (var permit in permits) await PermitService.ReleasePermitAsync(db, permit, time.GetUtcNow(), ct);
        group.Deleted = true;
    }
}
