using Raisin.EventSystem;

namespace Raisin.EventSystem.Tests.Helpers;

public class EventCapture<T> : IEventSubscriber<T> where T : EventSystemEventArgs
{
    private readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<T> _received = [];
    private readonly object _lock = new();

    public IReadOnlyList<T> Received
    {
        get { lock (_lock) return _received.ToList(); }
    }

    public void ExecuteEvent(object sender, T eventArgs)
    {
        lock (_lock)
            _received.Add(eventArgs);
        _tcs.TrySetResult(eventArgs);
    }

    public Task<T> WaitForEvent(TimeSpan? timeout = null)
    {
        var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        cts.Token.Register(() => _tcs.TrySetCanceled());
        return _tcs.Task;
    }

    public void DestroySubscriber() { }
}
