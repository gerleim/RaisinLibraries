# Raisin.EventSystem

Lightweight, thread-safe event bus for .NET 8 applications with automatic UI thread marshalling.

## Features

- **Publish-subscribe** pattern with type-safe event args
- **Automatic UI thread marshalling** — subscribers registered on the UI thread receive callbacks on the UI thread, no manual dispatching needed
- **Filtered subscriptions** — subscribe with a predicate to receive only matching events
- **Keyed events** — route multiple event variants of the same type to different handlers
- **Dual dispatch modes** — synchronous (`Invoke`) for ordered signals, async via thread pool (`InvokeOnThreadPool`) for fire-and-forget
- **Resilient** — one subscriber throwing does not affect others; errors are routed to an `OnError` handler
- **Zero external dependencies**

## Installation

```
dotnet add package Raisin.EventSystem
```

## Quick start

### Basic publish and subscribe

```csharp
var events = new EventSystem();

// Subscribe
events.Subscribe(mySubscriber); // implements IEventSubscriber<MessageArgs>

// Publish
events.Message(this, "Something happened", MessageSeverity.Info, "Category");
```

### Implementing a subscriber

```csharp
public class MyService : IEventSubscriber<MessageArgs>
{
    public void ExecuteEvent(object sender, MessageArgs e)
    {
        Console.WriteLine($"[{e.Severity}] {e.Message}");
    }

    public void DestroySubscriber() { /* cleanup */ }
}
```

### Bulk subscription

```csharp
// Registers all IEventSubscriber<T> interfaces on the object
events.SubscribeAll(myService);

// Unregisters all at once
events.DestroyAll(myService);
```

### Filtered subscription

```csharp
events.Subscribe(mySubscriber, filter: e => e.Severity >= MessageSeverity.Warning);
```

## Built-in event types

| Type | Fields | Purpose |
|------|--------|---------|
| `MessageArgs` | Message, Severity, Category, Subcategory | General application messages |
| `LogArgs` | Message, Severity, Category, Subcategory | File/diagnostic logging |
| `AlertArgs` | Type, Message, Symbol, Category | System and domain alerts |

Severity levels: `Detail`, `Verbose`, `Info`, `Warning`, `Error`, `Critical`

## License

MIT
