# Raisin.Core

Shared core utilities for .NET 8 desktop applications — file logging, atomic writes, credential protection, and process lifecycle management.

## Features

- **SafeFile** — Atomic file I/O via temp-file-and-replace, preventing corruption on crash or power loss
- **FileLogger** — Rolling daily file logger that subscribes to [Raisin.EventSystem](https://www.nuget.org/packages/Raisin.EventSystem) events, with configurable retention and automatic cleanup
- **DpapiStringProtector** — Windows DPAPI-based encryption for storing credentials and secrets per-user (implements `IStringProtector`)
- **AppPaths** — Centralized application path management with support for portable and roaming (AppData) modes, including migration between the two
- **ParentProcessWatcher** — Monitor a parent process lifetime via `--parent-pid` command-line argument and react when it exits
- **DoubleExtensions** — Epsilon-based floating-point comparison (`ApproxEquals`) for layout and rendering calculations

## Installation

```
dotnet add package Raisin.Core
```

## Quick start

### Atomic file writes

```csharp
SafeFile.WriteAllText("config.json", jsonString);
```

### Event-driven file logging

```csharp
var events = new EventSystem();
var logger = new FileLogger(events, "logs/myapp.log", retentionDays: 30);

events.Log(this, "Application started", LogTarget.File, LogSeverity.Info);
```

### Credential protection (Windows)

```csharp
IStringProtector protector = new DpapiStringProtector();
string encrypted = protector.Protect("my-api-key");
string decrypted = protector.Unprotect(encrypted);
```

### Application paths

```csharp
AppPaths.Configure("MyApp");          // sets up %AppData%/MyApp
AppPaths.SetPortable(true);           // switch to app-local storage
string dataDir = AppPaths.DataDir;    // current data directory
```

## Dependencies

- [Raisin.EventSystem](https://www.nuget.org/packages/Raisin.EventSystem) — event bus for logging integration
- [System.Security.Cryptography.ProtectedData](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData) — Windows DPAPI

## License

MIT
