using System.ComponentModel;

namespace Raisin.StyleInspector;

public class TemplateTriggerInfo : INotifyPropertyChanged
{
    private bool? _isActive;

    public required string Source { get; init; }
    public required string Condition { get; init; }
    public required List<string> Setters { get; init; }
    public required List<string> PropertyNames { get; init; }
    internal Func<bool?>? Evaluator { get; init; }

    public bool? IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveIndicator)));
            }
        }
    }

    public string ActiveIndicator => IsActive switch
    {
        true => "●",
        false => "○",
        null => "◌",
    };

    public string SettersSummary => $"→ {Setters.Count} setter(s)";
    public string SettersTooltip => string.Join("\n", Setters);

    public void Refresh()
    {
        if (Evaluator == null) return;
        try { IsActive = Evaluator(); }
        catch { IsActive = null; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
