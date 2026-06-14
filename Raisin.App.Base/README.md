# Raisin.App.Base

Application infrastructure for .NET 8 desktop apps — environment abstraction with dependency injection, binary search on sorted collections, text filtering, and timezone utilities.

## Features

- **AppEnvironment / IAppEnvironment** — Non-static, injectable service for resolving data directories (AppData, LocalAppData, app-local, temp) per application name
- **ServiceCollectionExtensions** — `AddAppEnvironment("MyApp")` registers the environment in `Microsoft.Extensions.DependencyInjection`
- **BarSearch** — Generic binary search on sorted `IReadOnlyList<T>` with `LowerBound`, `LastLe`, and `Closest` methods
- **TextFilter** — Case-insensitive text filter with wildcard support (`%`, `_`, `?`) and negation (`!` prefix)
- **TimeUtils** — US Eastern timezone conversions and bar-boundary alignment for time-series data
- **DataCategory** — Enum for categorizing application data storage locations
- **IManagedPaths** — Contract for components that manage named file paths within a data category

## Installation

```
dotnet add package Raisin.App.Base
```

## Quick start

### Environment and DI setup

```csharp
var services = new ServiceCollection();
services.AddAppEnvironment("MyApp");
var provider = services.BuildServiceProvider();

var env = provider.GetRequiredService<IAppEnvironment>();
string dataDir = env.Resolve(DataCategory.AppData);
```

### Binary search on time-sorted data

```csharp
int idx = BarSearch.Closest(bars, targetTime, b => b.Time);
int first = BarSearch.LowerBound(bars, startTime, b => b.Time);
int last = BarSearch.LastLe(bars, endTime, b => b.Time);
```

### Text filtering with wildcards

```csharp
var filter = new TextFilter();
filter.Update("AAPL%");       // wildcard: matches "AAPL", "AAPL.US", etc.
filter.Update("!AAPL");       // negation: matches everything except "AAPL"
bool matches = filter.Matches("AAPL");
```

### Timezone conversion

```csharp
DateTime eastern = TimeUtils.ConvertUtcToEastern(DateTime.UtcNow);
DateTime aligned = TimeUtils.AlignToBarBoundary(eastern, barSeconds: 300);
```

## Dependencies

- [Raisin.Core](https://www.nuget.org/packages/Raisin.Core) — app paths, atomic file I/O
- [Raisin.EventSystem](https://www.nuget.org/packages/Raisin.EventSystem) — event bus
- [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) — DI container

## License

MIT
