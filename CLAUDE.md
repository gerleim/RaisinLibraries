# RaisinLibraries

## Build
```
dotnet build RaisinLibraries.slnx
```

## Test
```
dotnet test RaisinLibraries.slnx
```

## Project Structure

| Library | Target | Description |
|---------|--------|-------------|
| **Raisin.EventSystem** | net8.0 | Event bus with UI thread marshalling, filtered/keyed subscriptions |
| **Raisin.Core** | net8.0 | File logging, atomic writes (SafeFile), DPAPI credentials, app paths |
| **Raisin.App.Base** | net8.0 | Environment abstraction, DI wiring, binary search, text filtering |
| **Raisin.WPF.Base** | net8.0-windows | MVVM base classes, window management, docking layout, settings |

Dependency chain: `EventSystem → Core → App.Base → WPF.Base`

Test projects are under `Tests/`, one per library.

## Conventions
- File-scoped namespaces
- Implicit usings and nullable enabled
- Default namespace per project (no folder-based sub-namespaces)
- Minimize external NuGet dependencies
- Test framework: xUnit v3 (`xunit.v3` + `xunit.runner.visualstudio` v3 + `Microsoft.NET.Test.Sdk`), test projects are executables (`OutputType=Exe`)
- Per-project READMEs serve as NuGet `PackageReadmeFile` — keep them maintained

## Tool Usage
- Do NOT use `git -C <path>` — the working directory is already the repo root, so use `git` commands directly (e.g., `git show`, `git log`). The `-C` flag breaks auto-allow permission patterns.
- Do NOT prefix Bash commands with `cd` — auto-allow patterns match from the start of the command, so `cd ... &&` prefixes break pattern matching. Use absolute paths instead.
- Do NOT pass absolute paths to `find`, `grep`, `rg`, or similar commands when the working directory is already the repo root — use `.` or relative paths instead. Absolute paths break auto-allow permission patterns and trigger unnecessary permission prompts.

## Commit Preferences
- Do NOT add `Co-Authored-By` lines to commits
