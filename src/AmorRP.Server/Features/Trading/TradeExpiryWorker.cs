using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Trading;

public sealed class TradeExpiryWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<TradeExpiryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
                var reservations = scope.ServiceProvider.GetRequiredService<TradeReservations>();
                var now = time.GetUtcNow();
                var communities = await db.Trades.Where(x => (x.State == "invited" || x.State == "open" || x.State == "confirming") && x.ExpiresAt <= now)
                    .Select(x => x.CommunityId).Distinct().Take(100).ToArrayAsync(stoppingToken);
                foreach (var community in communities)
                {
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                    await CommandStore.LockAsync(db, ["community:" + community], stoppingToken);
                    await reservations.ExpireAsync(community, stoppingToken); await db.SaveChangesAsync(stoppingToken); await tx.CommitAsync(stoppingToken);
                    db.ChangeTracker.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogWarning(ex, "Trade expiry sweep failed; next sweep will retry."); }
        }
    }
}
