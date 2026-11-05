using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Billing;

public sealed class DebtCollectionWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<DebtCollectionWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), time);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
                var settlement = scope.ServiceProvider.GetRequiredService<DebtSettlement>();
                var pending = await db.Memberships.AsNoTracking().Where(x => x.CollectionPending)
                    .OrderBy(x => x.LastCollectionAttempt).ThenBy(x => x.CharacterId).Take(16).Select(x => new { x.CommunityId, x.CharacterId }).ToArrayAsync(ct);
                foreach (var item in pending)
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    await CommandStore.LockAsync(db, ["community:" + item.CommunityId], ct);
                    var member = await db.Memberships.SingleAsync(x => x.CommunityId == item.CommunityId && x.CharacterId == item.CharacterId, ct);
                    if (member.CollectionPending) await settlement.CollectAsync(member, "automatic", null, ct);
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogWarning(ex, "Debt collection failed; the next sweep will retry. Held funds remain unavailable."); }
        }
    }
}
