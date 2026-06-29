using System.ComponentModel;

namespace Raisin.WPF.Base.Controls;

public class SelectableFilterItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Label { get; }
    public object? Value { get; }

    public bool SuppressChangeNotification { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            if (!SuppressChangeNotification)
                SelectionChanged?.Invoke();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? SelectionChanged;

    public SelectableFilterItem(string label, object? value)
    {
        Label = label;
        Value = value;
    }

    public override string ToString() => Label;

    public static List<SelectableFilterItem> FromEnum<T>(Action? onChanged = null) where T : struct, Enum
    {
        var items = new List<SelectableFilterItem>();
        foreach (var val in Enum.GetValues<T>())
        {
            var item = new SelectableFilterItem(val.ToString(), val);
            if (onChanged != null)
                item.SelectionChanged += onChanged;
            items.Add(item);
        }
        return items;
    }
}
