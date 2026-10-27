namespace AmorRP.Plugin.Services.Api;

// Owned by the UI thread. Network tasks return callbacks; only collected,
// current results may change the view. Independent feature reads use its token
// and data generation through BackgroundRefresh<T>.
internal sealed class RequestCoordinator : IDisposable
{
    private sealed record Pending(Task<Action> Task, long View, long Data);
    private Pending? foreground, synchronization;
    private CancellationTokenSource lifetime = new();

    public long ViewGeneration { get; private set; }
    public long DataGeneration { get; private set; }
    public CancellationToken Token => lifetime.Token;
    public bool ForegroundBusy => foreground != null;
    public bool SynchronizationBusy => synchronization != null;

    public void ChangeView() => ViewGeneration++;
    public void ChangeData() => DataGeneration++;

    public Task? StartForeground(Func<CancellationToken, Task<Action>> run)
    {
        if (foreground != null) return null;
        foreground = Start(run); return foreground.Task;
    }
    public Task StartSynchronization(Func<CancellationToken, Task<Action>> run)
    {
        if (synchronization == null) synchronization = Start(run);
        return synchronization.Task;
    }
    private Pending Start(Func<CancellationToken, Task<Action>> run)
    {
        var token = lifetime.Token;
        return new(Task.Run(() => run(token), token), ViewGeneration, DataGeneration);
    }

    public bool TakeForeground(out Task<Action> completed, out bool stale) => Take(ref foreground, false, false, out completed, out stale);
    public bool TakeSynchronization(bool renewingSession, out Task<Action> completed, out bool stale) =>
        Take(ref synchronization, true, renewingSession, out completed, out stale);

    private bool Take(ref Pending? pending, bool checkData, bool defer, out Task<Action> completed, out bool stale)
    {
        completed = null!; stale = false;
        if (defer || pending?.Task.IsCompleted != true) return false;
        var result = pending; pending = null; completed = result.Task;
        stale = result.View != ViewGeneration || checkData && result.Data != DataGeneration;
        if (stale) _ = completed.Exception;
        return true;
    }

    public void Reset()
    {
        ChangeView(); ChangeData();
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        Abandon(ref foreground); Abandon(ref synchronization);
    }
    private static void Abandon(ref Pending? pending)
    {
        if (pending != null)
            _ = pending.Task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        pending = null;
    }
    public void Dispose()
    {
        Reset(); lifetime.Dispose();
    }
}
