using System.Windows.Input;

namespace Raisin.WPF.Base;

/// <summary>
/// ICommand implementation for WPF MVVM data binding. Wraps an execute delegate
/// and an optional can-execute predicate so ViewModels can expose commands without
/// deriving custom ICommand classes.
/// <para>
/// Button enabled/disabled state refreshes through two complementary mechanisms:
/// <list type="bullet">
///   <item><b>CommandManager.RequerySuggested</b> — WPF fires this automatically on
///   focus changes, input events, and property changes. Runs at Background priority,
///   so it can be starved when the UI thread is busy (e.g., during rapid market-data updates).</item>
///   <item><b>RaiseCanExecuteChanged()</b> — Call this explicitly when you know the
///   can-execute state has changed and need an immediate UI update.</item>
/// </list>
/// </para>
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// Convenience constructor for parameter-less commands.
    /// </summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    /// <summary>
    /// Creates a command with a typed parameter delegate and optional can-execute predicate.
    /// </summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Local backing event so <see cref="RaiseCanExecuteChanged"/> can fire CanExecuteChanged
    /// directly, without going through CommandManager's deferred Background-priority requery.
    /// </summary>
    private event EventHandler? _canExecuteChanged;

    /// <summary>
    /// Subscribes handlers to both <see cref="CommandManager.RequerySuggested"/> (automatic
    /// WPF requery) and the local <see cref="_canExecuteChanged"/> event (explicit raise),
    /// so either mechanism will refresh bound UI elements.
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add
        {
            CommandManager.RequerySuggested += value;
            _canExecuteChanged += value;
        }
        remove
        {
            CommandManager.RequerySuggested -= value;
            _canExecuteChanged -= value;
        }
    }

    /// <summary>
    /// Directly raises CanExecuteChanged, bypassing CommandManager's
    /// Background-priority scheduling which can be starved by continuous events.
    /// </summary>
    public void RaiseCanExecuteChanged() => _canExecuteChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Returns true if the command can execute; defaults to true when no predicate is provided.</summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>Invokes the wrapped execute delegate.</summary>
    public void Execute(object? parameter) => _execute(parameter);
}
