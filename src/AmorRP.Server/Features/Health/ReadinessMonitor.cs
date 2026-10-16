using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Health;

// One probe loop per process. HTTP requests only read the published value and
// cannot schedule, duplicate or cancel database checks. Initially fail closed.
public sealed class ReadinessMonitor(IServiceScopeFactory scopes, IOptions<HealthPolicyOptions> policy,
    TimeProvider time, ILogger<ReadinessMonitor> log) : BackgroundService
{
    private int ready;
    public bool IsReady => Volatile.Read(ref ready) == 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = false;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                result = await scope.ServiceProvider.GetRequiredService<DatabaseReadiness>().IsReadyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogWarning("Readiness probe failed ({ExceptionType}).", ex.GetType().Name); }
            Volatile.Write(ref ready, result ? 1 : 0);
            try { await Task.Delay(TimeSpan.FromSeconds(policy.Value.ReadinessRefreshSeconds), time, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
