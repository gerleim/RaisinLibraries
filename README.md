# RaisinLibraries

Shared .NET 8 libraries for the Raisin application family.

## Libraries

| Library | Target | Description |
|---------|--------|-------------|
| [Raisin.EventSystem](Raisin.EventSystem/) | net8.0 | Lightweight event bus with UI thread marshalling, filtered subscriptions, and keyed events |
| [Raisin.Core](Raisin.Core/) | net8.0 | File logging, atomic writes (SafeFile), credential protection (DPAPI), app path management |
| [Raisin.App.Base](Raisin.App.Base/) | net8.0 | Application infrastructure — environment abstraction, DI wiring, binary search, text filtering |
| [Raisin.WPF.Base](Raisin.WPF.Base/) | net8.0-windows | WPF foundation — MVVM base classes, window management, docking layout, settings infrastructure |

## Dependency chain

```
Raisin.EventSystem          (zero dependencies)
    |
Raisin.Core                 (+ System.Security.Cryptography.ProtectedData)
    |
Raisin.App.Base             (+ Microsoft.Extensions.DependencyInjection)
    |
Raisin.WPF.Base             (+ MaterialDesignThemes, Raisin.AvalonDock)
```

## Build and test

`RaisinLibraries.slnx` exists so `dotnet build` and `dotnet test` work from the repo root. In practice, these libraries are consumed as project references from application solutions.

```
dotnet build RaisinLibraries.slnx
dotnet test RaisinLibraries.slnx
```

Tests are under `Tests/` with one project per library:

- `Tests/Raisin.EventSystem.Tests/` — event bus subscribe, filter, destroy
- `Tests/Raisin.App.Base.Tests/` — BarSearch binary search algorithms

## License

MIT
