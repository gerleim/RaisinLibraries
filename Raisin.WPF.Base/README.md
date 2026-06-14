# Raisin.WPF.Base

Shared WPF base library for .NET 8 desktop applications — MVVM foundation, window management, docking layout persistence, data grid behaviors, settings infrastructure, and dark theme support.

## Features

### MVVM

- **ViewModelBase** — `INotifyPropertyChanged` base with `SetProperty`, batch notification, and UI thread helpers
- **ToolWindowViewModel** — Base for tabbed/dockable tool windows with title deduplication, visibility tracking, and close support
- **RelayCommand** — Simple `ICommand` implementation with `CanExecute` support

### Window management

- **WindowPlacementHelper** — Capture, restore, and validate window position across monitor configurations
- **DarkWindowHelper** — Apply Windows 11 dark mode to window chrome (title bar, background) with WM_ERASEBKGND interception to prevent white flash
- **DockLayoutHelper** — Save and restore AvalonDock layouts to XML with atomic file writes

### Data grid

- **DataGridColumnBehavior** — Attached behavior for auto-fit and persistent column widths with right-click context menu
- **AdaptiveHeader** — Column headers that switch between full and abbreviated text based on available width
- **GridSettingsService** — Persists column widths to JSON with in-memory cache

### Settings infrastructure

- **SettingDefinition** — Declarative metadata for settings (category, editor type, bounds, choices)
- **SettingItemViewModel** hierarchy — Concrete view models for Bool, Int, Double, String, TimeOnly, Choice, IntList, and compound types (Offset, TimePair, IntPair, DoublePair)
- **SettingItemFactory** — Creates the right view model from a definition
- **SettingItemTemplateSelector** — WPF DataTemplateSelector for rendering each setting type

### Other utilities

- **LargeNumberConverter** — IValueConverter formatting numbers as 1.5B, 2.3M, 4.7K
- **NumericInputBehavior** — Attached behavior restricting TextBox input to integers or decimals

## Installation

```
dotnet add package Raisin.WPF.Base
```

## Quick start

### View model

```csharp
public class MyViewModel : ViewModelBase
{
    private string _title = "";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value, nameof(Title));
    }
}
```

### Dark window

```csharp
protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);
    DarkWindowHelper.Apply(this);
}
```

### Persistent column widths

```xaml
<DataGrid local:DataGridColumnBehavior.GridId="MyGrid" ... />
```

## Dependencies

- [Raisin.Core](https://www.nuget.org/packages/Raisin.Core) — atomic file I/O, app paths
- [Raisin.EventSystem](https://www.nuget.org/packages/Raisin.EventSystem) — event bus
- [Raisin.AvalonDock](https://www.nuget.org/packages/Raisin.AvalonDock) — docking framework
- [MaterialDesignThemes](https://www.nuget.org/packages/MaterialDesignThemes) — Material Design resources

## License

MIT
