using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class DatabaseReadiness(AmorDbContext db, ILogger<DatabaseReadiness> logger)
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            // A SELECT 1 alone would report an unmigrated or incompatible DB as ready.
            if (!await db.Database.CanConnectAsync(timeout.Token))
                return false;
            if ((await db.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
                return false;
            var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            if ((await db.Database.GetAppliedMigrationsAsync(timeout.Token)).Any(x => !known.Contains(x)))
                return false;
            return await db.InfrastructureStates.AsNoTracking()
                .AnyAsync(x => x.Id == "bootstrap" && x.SchemaVersion == 1, timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // No exception message or connection details in health output/logs.
            logger.LogWarning("Database readiness check failed ({ExceptionType}).", ex.GetType().Name);
            return false;
        }
    }
}
