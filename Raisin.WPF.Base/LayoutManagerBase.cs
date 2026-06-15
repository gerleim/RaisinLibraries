using System.IO;
using System.Text.Json;
using System.Windows;
using AvalonDock;
using Raisin.App.Base;
using Raisin.Core;
using Raisin.WPF.Base.Models;

namespace Raisin.WPF.Base;

public abstract class LayoutManagerBase : IManagedPaths
{
    protected static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static Dictionary<string, FloatingWindowBounds> _floatingDefaults = new();

    protected readonly IAppEnvironment Environment;
    protected readonly DockingManager DockingManager;
    protected readonly Window MainWindow;

    public bool IsRestoringLayout { get; protected set; }

    public virtual DataCategory Category => DataCategory.AppData;
    public IReadOnlyList<string> ManagedNames => [StateFileName, DockFileName];

    public abstract string StateFileName { get; }
    public virtual string DockFileName => Path.ChangeExtension(StateFileName, ".xml");

    protected string StatePath => Path.Combine(Environment.Resolve(Category), StateFileName);
    protected string DockLayoutPath => Path.ChangeExtension(StatePath, ".xml");

    protected LayoutManagerBase(IAppEnvironment environment, DockingManager dockingManager, Window mainWindow)
    {
        Environment = environment;
        DockingManager = dockingManager;
        MainWindow = mainWindow;
    }

    // --- Static utilities (usable before instance creation) ---

    public static void RememberFloatingBounds(string typeName, double left, double top, double width, double height)
        => _floatingDefaults[typeName] = new FloatingWindowBounds(left, top, width, height);

    public static FloatingWindowBounds? GetFloatingDefaults(string typeName)
        => _floatingDefaults.GetValueOrDefault(typeName);

    public static TState? LoadState<TState>(string jsonPath) where TState : class
    {
        try
        {
            if (!File.Exists(jsonPath))
                return null;
            var json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<TState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void RestoreWindowPlacement(Window window, AppLayoutState state)
    {
        var p = WindowPlacementHelper.FromNullable(
            state.WindowLeft, state.WindowTop, state.WindowWidth, state.WindowHeight, state.WindowMaximized);
        if (p is not null)
            WindowPlacementHelper.Restore(window, p);
    }

    protected static Dictionary<string, FloatingWindowBounds> FloatingDefaults
    {
        get => _floatingDefaults;
        set => _floatingDefaults = value;
    }

    // --- Save (template method) ---

    public void Save() => Save(StatePath);

    public virtual void Save(string jsonPath)
    {
        try
        {
            var xmlPath = Path.ChangeExtension(jsonPath, ".xml");
            DockLayoutHelper.SaveDockLayout(DockingManager, xmlPath);

            var wp = WindowPlacementHelper.Capture(MainWindow);
            var state = CaptureState(wp);
            var json = JsonSerializer.Serialize(state, state.GetType(), JsonOptions);
            SafeFile.WriteAllText(jsonPath, json);
        }
        catch { }
    }

    protected abstract AppLayoutState CaptureState(WindowPlacement wp);

    // --- Restore helpers ---

    protected bool RestoreDockLayout(Func<string, object?> contentResolver, string? dockLayoutPath = null)
    {
        var xmlPath = dockLayoutPath ?? DockLayoutPath;
        return DockLayoutHelper.RestoreDockLayout(DockingManager, contentResolver, xmlPath);
    }

    // --- Virtual hooks for apps with Documents/Anchorables ---

    public virtual void CloseAll() { }

    public virtual void RebindDockSources() { }
}
