namespace AmorRP.Server.Features.Authentication;

public sealed class LoginOriginCleanupWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<LoginOriginCleanupWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<LoginAdmission>().ClearExpiredOriginsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            // No address, fingerprint, database details or secret in routine logs.
            catch (Exception ex) { log.LogWarning("Login origin cleanup failed ({ExceptionType}); next sweep will retry.", ex.GetType().Name); }
        }
    }
}
