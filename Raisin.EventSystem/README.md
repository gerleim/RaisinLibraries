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
events.Subscribe(mySubscriber); // implements IEventSubscriber<LogArgs>

// Publish — LogTarget controls where the entry goes
events.Log(this, "Connected to server", LogTarget.UI);              // UI + file
events.Log(this, "Socket buffer: 4096", LogTarget.File);            // file only
events.Log(this, "Retrying...", LogTarget.UI, LogSeverity.Warning); // with severity
```

### Implementing a subscriber

```csharp
public class MyService : IEventSubscriber<LogArgs>
{
    public void ExecuteEvent(object sender, LogArgs e)
    {
        Console.WriteLine($"[{e.LogSeverity}] {e.Message}");
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
events.Subscribe(mySubscriber, filter: e => e.LogSeverity >= LogSeverity.Warning);
```

## Built-in event types

| Type | Fields | Purpose |
|------|--------|---------|
| `LogArgs` | Message, LogSeverity, Target, Category, Subcategory | Unified logging (UI and/or file) |
| `AlertArgs` | Type, Message, Symbol, Category | System and domain alerts |

`LogTarget`: `File` (file only), `UI` (UI + file)

`LogSeverity`: `Detail`, `Verbose`, `Info`, `Warning`, `Error`, `Critical`

> `MessageArgs` and `MessageSeverity` are deprecated — use `LogArgs` with `LogTarget.UI` instead.

## License

MIT
