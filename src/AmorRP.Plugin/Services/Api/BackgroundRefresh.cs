namespace AmorRP.Plugin.Services.Api;

// One feature's read lifecycle. Values are applied by its owner on the UI thread.
internal sealed class BackgroundRefresh<T>(TimeSpan interval) : IDisposable
{
    private Task<T>? pending;
    private CancellationTokenSource? cancellation;
    private long generation;
    private DateTimeOffset nextRead;

    public Exception? Error { get; private set; }
    public bool IsLoading => pending != null;
    public bool HasLoaded { get; private set; }

    public void Start(DateTimeOffset now, long currentGeneration, CancellationToken lifetime, Func<CancellationToken, Task<T>> load)
    {
        if (pending != null || now < nextRead || lifetime.IsCancellationRequested) return;
        generation = currentGeneration;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        try { pending = load(cancellation.Token); }
        catch (Exception error) { pending = Task.FromException<T>(error); }
    }

    public bool TryCollect(DateTimeOffset now, long currentGeneration, out T value)
    {
        value = default!;
        if (generation != currentGeneration)
        {
            Invalidate();
            Error = null;
            generation = currentGeneration;
        }
        if (pending is not { IsCompleted: true }) return false;
        var completed = pending;
        pending = null;
        var cancelled = cancellation!.IsCancellationRequested;
        cancellation.Dispose();
        cancellation = null;
        if (cancelled || generation != currentGeneration || completed.IsCanceled)
        {
            _ = completed.Exception;
            nextRead = default;
            return false;
        }
        if (completed.IsCompletedSuccessfully)
        {
            value = completed.Result;
            Error = null;
            HasLoaded = true;
            nextRead = now + interval;
            return true;
        }
        Error = completed.Exception!.GetBaseException();
        nextRead = now.AddSeconds(15);
        return false;
    }

    public void Invalidate()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        if (pending != null)
            _ = pending.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        pending = null;
        nextRead = default;
    }

    public void Reset()
    {
        Invalidate();
        Error = null;
        HasLoaded = false;
    }

    public void Dispose() => Reset();
}
