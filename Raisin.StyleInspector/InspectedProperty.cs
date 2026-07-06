using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Raisin.StyleInspector;

public class InspectedProperty : INotifyPropertyChanged
{
    private object? _value;
    private string _displayValue = "";
    private BaseValueSource _source;
    private string _sourceTag = "";
    private string _sourceTooltip = "";
    private Brush _sourceBrush = Brushes.Gray;
    private Brush? _colorPreview;
    private bool _isEdited;

    public required string Name { get; init; }
    public required string Category { get; init; }
    public required DependencyProperty Property { get; init; }
    public required DependencyObject Element { get; init; }
    public string? ResourceKey { get; init; }
    public string? StyleOrigin { get; set; }
    public string? OriginDetail { get; set; }

    public object? Value
    {
        get => _value;
        set { if (_value != value) { _value = value; OnPropertyChanged(); OnPropertyChanged(nameof(BoolValue)); } }
    }

    public string DisplayValue
    {
        get => _displayValue;
        set { if (_displayValue != value) { _displayValue = value; OnPropertyChanged(); } }
    }

    public BaseValueSource Source
    {
        get => _source;
        set { _source = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDefault)); }
    }

    public string SourceTag
    {
        get => _sourceTag;
        set { _sourceTag = value; OnPropertyChanged(); }
    }

    public string SourceTooltip
    {
        get => _sourceTooltip;
        set { _sourceTooltip = value; OnPropertyChanged(); }
    }

    public Brush SourceBrush
    {
        get => _sourceBrush;
        set { _sourceBrush = value; OnPropertyChanged(); }
    }

    public Brush? ColorPreview
    {
        get => _colorPreview;
        set { _colorPreview = value; OnPropertyChanged(); }
    }

    public bool IsEdited
    {
        get => _isEdited;
        set { _isEdited = value; OnPropertyChanged(); }
    }

    public bool IsDefault => Source == BaseValueSource.Default;
    public bool IsBoolType => Property.PropertyType == typeof(bool);
    public bool IsEnumType => Property.PropertyType.IsEnum;
    public bool IsTextType => !IsBoolType && !IsEnumType;
    public bool? BoolValue => Value as bool?;
    public Array? EnumValues => Property.PropertyType.IsEnum ? Enum.GetValues(Property.PropertyType) : null;

    public bool ApplyText(string text)
    {
        var parsed = PropertyEnumerator.ParseValue(text, Property.PropertyType);
        if (parsed == null) return false;
        ApplyValue(parsed);
        return true;
    }

    public void ApplyValue(object? newValue)
    {
        try
        {
            Element.SetValue(Property, newValue);
            IsEdited = true;
            Refresh();
        }
        catch { }
    }

    public void ResetValue()
    {
        try
        {
            Element.ClearValue(Property);
            IsEdited = false;
            Refresh();
        }
        catch { }
    }

    public void Refresh()
    {
        var value = Element.GetValue(Property);
        var source = DependencyPropertyHelper.GetValueSource(Element, Property);
        Value = value;
        DisplayValue = PropertyEnumerator.FormatValue(value, ResourceKey);
        Source = source.BaseValueSource;
        SourceTag = PropertyEnumerator.GetSourceTag(source.BaseValueSource);
        SourceTooltip = PropertyEnumerator.GetSourceTooltip(source.BaseValueSource);
        SourceBrush = PropertyEnumerator.GetSourceBrush(source.BaseValueSource);
        ColorPreview = PropertyEnumerator.GetColorPreview(value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
