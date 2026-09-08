namespace Raisin.EventSystem;

public class EventHandler<T> : IEventHandler where T : EventSystemEventArgs
{
    private readonly record struct Entry(
        IEventSubscriber<T> Subscriber,
        SynchronizationContext? Context,
        Func<T, bool>? Filter,
        DispatchClass? Class = null);

    private readonly List<Entry> _subscribers = new();
    private readonly object _sync = new();
    private volatile Entry[] _snapshot = [];

    internal Action<Exception>? OnError { get; set; }

    public void Add(IEventSubscriber<T> subscriber, SynchronizationContext? context, Func<T, bool>? filter = null,
        DispatchClass? dispatchClass = null)
    {
        lock (_sync)
        {
            _subscribers.Add(new(subscriber, context, filter, dispatchClass));
            _snapshot = [.. _subscribers];
        }
    }

    public bool Remove(IEventSubscriber<T> subscriber)
    {
        lock (_sync)
        {
            _subscribers.RemoveAll(e => e.Subscriber == subscriber);
            _snapshot = [.. _subscribers];
            return _subscribers.Count == 0;
        }
    }

    /// <summary>
    /// Dispatches the event to all subscribers. If a subscriber was registered with a
    /// <see cref="SynchronizationContext"/> (captured at subscribe time), the callback is
    /// posted via <c>context.Post()</c>, auto-marshalling to the subscriber's thread.
    /// WPF subscribers registered on the UI thread receive their <c>ExecuteEvent</c> call
    /// on the UI thread automatically — no manual Dispatcher check is needed.
    /// </summary>
    public void Invoke(object sender, T eventArgs)
    {
        var snapshot = _snapshot;
        foreach (var entry in snapshot)
        {
            try
            {
                if (entry.Filter != null && !entry.Filter(eventArgs))
                    continue;

                switch (entry.Class)
                {
                    // Declared: the subscriber said where it wants to run, and the producer's choice
                    // of Invoke or InvokeOnThreadPool no longer decides it.
                    case DispatchClass.State:
                        entry.Subscriber.ExecuteEvent(sender, eventArgs);
                        break;
                    case DispatchClass.Pool:
                        var poolEntry = entry;
                        Task.Run(() =>
                        {
                            try { poolEntry.Subscriber.ExecuteEvent(sender, eventArgs); }
                            catch (Exception ex) { OnError?.Invoke(ex); }
                        });
                        break;
                    case DispatchClass.Notify:
                        if (entry.Context is null)
                            throw new InvalidOperationException(
                                $"{entry.Subscriber.GetType().Name} subscribed to {typeof(T).Name} as " +
                                $"{nameof(DispatchClass.Notify)} without a SynchronizationContext.");
                        entry.Context.Post(_ => entry.Subscriber.ExecuteEvent(sender, eventArgs), null);
                        break;

                    // Undeclared: whatever this subscriber has always done.
                    default:
                        if (entry.Context != null)
                            entry.Context.Post(_ => entry.Subscriber.ExecuteEvent(sender, eventArgs), null);
                        else
                            entry.Subscriber.ExecuteEvent(sender, eventArgs);
                        break;
                }
            }
            catch (Exception ex)
            {
                // One subscriber failing must not kill others
                OnError?.Invoke(ex);
            }
        }
    }

    public bool IsEmpty => _snapshot.Length == 0;

    public void DestroyAllSubscribers()
    {
        Entry[] snapshot;
        lock (_sync)
        {
            snapshot = [.. _subscribers];
            _subscribers.Clear();
            _snapshot = [];
        }
        foreach (var entry in snapshot)
            entry.Subscriber.DestroySubscriber();
    }
}
