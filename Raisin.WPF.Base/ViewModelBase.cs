using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Raisin.WPF.Base;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertiesChanged(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Dispatches to UI thread if needed, otherwise runs inline.
    /// Use ONLY for plain C# event handlers (Action, event) and async callbacks (Task, Timer)
    /// that bypass EventSystem. Do NOT use inside ExecuteEvent methods — EventSystem auto-marshals those.
    /// </summary>
    public static void EnsureUIThread(Action action)
    {
        if (!RunOnUI(action))
            action();
    }

    /// <summary>
    /// If called from a background thread, dispatches <paramref name="action"/> to the
    /// UI thread and returns <c>true</c>. Returns <c>false</c> when already on the UI thread.
    /// Usage: <c>if (RunOnUI(() => Method(args))) return;</c>
    /// Use ONLY for plain C# event handlers and async callbacks that bypass EventSystem.
    /// Do NOT use inside ExecuteEvent methods — EventSystem auto-marshals those.
    /// </summary>
    public static bool RunOnUI(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
        {
            d.BeginInvoke(action);
            return true;
        }
        return false;
    }
}
