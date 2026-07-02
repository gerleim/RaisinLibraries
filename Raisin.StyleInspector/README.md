# Raisin.StyleInspector

Runtime WPF style debugging library. Inspect effective property values and their sources, compare elements side-by-side, edit values live, and export XAML setters.

## Quick start

Add a conditional reference so the inspector is DEBUG-only:

```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
  <PackageReference Include="Raisin.StyleInspector" Version="1.0.0" />
</ItemGroup>
```

Enable in `App.xaml.cs`:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
#if DEBUG
    Raisin.StyleInspector.StyleInspector.Enable(this);
#endif
}
```

Press `Ctrl+Shift+I` to open the inspector window.

## Options

```csharp
// Custom hotkey
StyleInspector.Enable(app, hotkey: new KeyGesture(Key.F12));

// Open immediately on startup
StyleInspector.Enable(app, autoOpen: true);
```
