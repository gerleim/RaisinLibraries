using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Raisin.EventSystem;

public class EventSystem : IDisposable
{
    public Action<Exception>? OnError { get; set; }
    public Action<string>? OnWarning { get; set; }

    public EventSystem()
    {
        SetSubscribeMethod();
        SetDestroyMethod();
    }

    private readonly ConcurrentDictionary<string, IEventHandler> _eventHandlers = new();

    /// <summary>
    /// Invokes the event synchronously on the caller's thread.
    /// Use for final assembled results and completion signals where ordering matters
    /// (e.g. historical data batch + end signal must arrive in sequence).
    /// Exceptions propagate directly to the caller.
    /// </summary>
    public void Invoke<T>(object sender, T eventArgs) where T : EventSystemEventArgs
    {
        var key = GetKey(eventArgs);

        if (_eventHandlers.TryGetValue(key, out var handler))
        {
            var handlerT = (EventHandler<T>)handler;
            handlerT.Invoke(sender, eventArgs);
        }
    }

    /// <summary>
    /// Invokes the event asynchronously on a thread pool thread via Task.Run.
    /// Use for external/intermediate events like IB API callbacks and individual ticks
    /// where fire-and-forget is acceptable and blocking the caller must be avoided.
    /// Exceptions are caught and routed to <see cref="OnError"/> instead of propagating.
    /// </summary>
    public void InvokeOnThreadPool<T>(object sender, T eventArgs) where T : EventSystemEventArgs
    {
        Task.Run(() =>
        {
            try
            {
                Invoke(sender, eventArgs);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        });
    }

    private static string GetKey<T>(T eventArgs) where T : EventSystemEventArgs
    {
        if (eventArgs == null)
            throw new ArgumentNullException(nameof(eventArgs));

        if (eventArgs is EventSystemEventWithArgumentsArgs args)
            return args.Key;
        else
            return GetKey<T>();
    }

    private static string GetKey<T>() where T : EventSystemEventArgs
    {
        return typeof(T).Name;
    }

    public void Subscribe<T>(IEventSubscriber<T> subscriber) where T : EventSystemEventArgs
    {
        Subscribe(subscriber, GetKey<T>());
    }

    public void Subscribe<T>(IEventSubscriber<T> subscriber, T eventArgs) where T : EventSystemEventWithArgumentsArgs
    {
        Subscribe(subscriber, GetKey(eventArgs));
    }

    public void Subscribe<T>(IEventSubscriber<T> subscriber, Func<T, bool> filter) where T : EventSystemEventArgs
    {
        Subscribe(subscriber, GetKey<T>(), filter);
    }

    public void Subscribe<T>(IEventSubscriber<T> subscriber, T eventArgs, Func<T, bool> filter) where T : EventSystemEventWithArgumentsArgs
    {
        Subscribe(subscriber, GetKey(eventArgs), filter);
    }

    private MethodInfo _subscribeMethod;
    private MethodInfo _destroyMethod;

    [MemberNotNull(nameof(_subscribeMethod))]
    private void SetSubscribeMethod()
    {
        _subscribeMethod = typeof(EventSystem)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == "Subscribe")
            .Where(m => m.IsGenericMethodDefinition)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 1 &&
                        parameters[0].ParameterType.IsGenericType &&
                        parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IEventSubscriber<>);
            });
    }

    [MemberNotNull(nameof(_destroyMethod))]
    private void SetDestroyMethod()
    {
        _destroyMethod = typeof(EventSystem)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == "DestroySubscriber")
            .Where(m => m.IsGenericMethodDefinition)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 1 &&
                        parameters[0].ParameterType.IsGenericType &&
                        parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IEventSubscriber<>);
            });
    }

    public void SubscribeAll(object subscriber)
    {
        var interfaces = subscriber.GetType()
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventSubscriber<>));

        foreach (var iface in interfaces)
        {
            var eventType = iface.GetGenericArguments()[0];
            var methodGeneric = _subscribeMethod!.MakeGenericMethod(eventType);
            methodGeneric.Invoke(this, [subscriber]);
        }
    }

    public void DestroyAll(object subscriber)
    {
        var interfaces = subscriber.GetType()
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventSubscriber<>));

        foreach (var iface in interfaces)
        {
            var eventType = iface.GetGenericArguments()[0];
            var methodGeneric = _destroyMethod!.MakeGenericMethod(eventType);
            methodGeneric.Invoke(this, [subscriber]);
        }
    }

    /// <summary>
    /// Captures <see cref="SynchronizationContext.Current"/> so that subscribers created on the
    /// UI thread receive their <c>ExecuteEvent</c> callbacks auto-marshalled to the UI thread.
    /// No manual Dispatcher check (RunOnUI/EnsureUIThread) is needed inside ExecuteEvent methods.
    /// </summary>
    private void Subscribe<T>(IEventSubscriber<T> subscriber, string key, Func<T, bool>? filter = null) where T : EventSystemEventArgs
    {
        var context = SynchronizationContext.Current;

        // UI-bound subscribers (INotifyPropertyChanged) should subscribe on the UI thread
        // so that SynchronizationContext is captured for auto-marshalling.
        // Fires OnWarning in production (wired in IBApp); silent in test environments.
        if (context is null && subscriber is INotifyPropertyChanged)
        {
            OnWarning?.Invoke($"EventSystem: {subscriber.GetType().Name} subscribes to {typeof(T).Name} " +
                              "without a SynchronizationContext. UI subscribers must subscribe on the UI thread.");
        }

        _eventHandlers.AddOrUpdate(key,
            _ =>
            {
                var handler = new EventHandler<T>();
                handler.OnError = OnError;
                handler.Add(subscriber, context, filter);
                return handler;
            },
            (_, existing) =>
            {
                var handlerT = (EventHandler<T>)existing;
                handlerT.OnError = OnError;
                handlerT.Add(subscriber, context, filter);
                return existing;
            });
    }

    public void DestroySubscriber<T>(IEventSubscriber<T> subscriber) where T : EventSystemEventArgs
    {
        DestroySubscriber(subscriber, GetKey<T>());
    }

    public void DestroySubscriber<T>(IEventSubscriber<T> subscriber, T eventArgs) where T : EventSystemEventWithArgumentsArgs
    {
        DestroySubscriber(subscriber, GetKey(eventArgs));
    }

    private void DestroySubscriber<T>(IEventSubscriber<T> subscriber, string key) where T : EventSystemEventArgs
    {
        if (subscriber == null)
            return;

        if (_eventHandlers.TryGetValue(key, out var handler))
        {
            var handlerT = (EventHandler<T>)handler;
            if (handlerT.Remove(subscriber))
                _eventHandlers.TryRemove(key, out _);
        }

        subscriber.DestroySubscriber();
    }

    public void Dispose()
    {
        foreach (var handler in _eventHandlers.Values)
            handler.DestroyAllSubscribers();
        _eventHandlers.Clear();
    }
}
