using System.Reflection;

namespace Raisin.WPF.Base.Settings;

public abstract class SettingItemViewModel : ViewModelBase
{
    public SettingDefinition Definition { get; }
    public string Key => Definition.Key;
    public string DisplayName => Definition.DisplayName;
    public string Description => Definition.Description;
    public string Category => Definition.Category;

    private readonly Func<object> _defaultFactory;

    private bool _isModified;
    public bool IsModified
    {
        get => _isModified;
        protected set => SetProperty(ref _isModified, value);
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public RelayCommand ResetCommand { get; }

    protected SettingItemViewModel(SettingDefinition definition, Func<object> defaultFactory)
    {
        Definition = definition;
        _defaultFactory = defaultFactory;
        ResetCommand = new RelayCommand(ResetToDefault);
    }

    public abstract void LoadFrom(object data);
    public abstract void ApplyTo(object data);
    public abstract void UpdateIsModified();

    public void ResetToDefault()
    {
        LoadFrom(_defaultFactory());
        UpdateIsModified();
    }

    protected object CreateDefault() => _defaultFactory();

    public virtual bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Key.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    protected static object? GetProperty(object data, string propertyPath)
    {
        object? current = data;
        foreach (var part in propertyPath.Split('.'))
        {
            if (current is null) return null;
            current = current.GetType().GetProperty(part)?.GetValue(current);
        }
        return current;
    }

    protected static PropertyInfo? GetPropertyInfo(object data, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        object? current = data;
        for (int i = 0; i < parts.Length - 1; i++)
            current = current?.GetType().GetProperty(parts[i])?.GetValue(current);
        return current?.GetType().GetProperty(parts[^1]);
    }

    protected static void SetPropertyValue(object data, string propertyPath, object? value)
    {
        var parts = propertyPath.Split('.');
        object? current = data;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            current = current?.GetType().GetProperty(parts[i])?.GetValue(current);
        }
        var prop = current?.GetType().GetProperty(parts[^1]);
        if (prop is not null && value is not null)
        {
            var converted = prop.PropertyType.IsEnum
                ? Enum.ToObject(prop.PropertyType, value)
                : Convert.ChangeType(value, prop.PropertyType);
            prop.SetValue(current, converted);
        }
    }
}

public class BoolSettingItem : SettingItemViewModel
{
    private bool _value;
    public bool Value
    {
        get => _value;
        set { if (SetProperty(ref _value, value)) UpdateIsModified(); }
    }

    public BoolSettingItem(SettingDefinition def, Func<object> defaultFactory) : base(def, defaultFactory) { }

    public override void LoadFrom(object data)
    {
        Value = (bool)(GetProperty(data, Definition.PropertyName) ?? false);
    }

    public override void ApplyTo(object data)
    {
        SetPropertyValue(data, Definition.PropertyName, Value);
    }

    public override void UpdateIsModified()
    {
        var def = (bool)(GetProperty(CreateDefault(), Definition.PropertyName) ?? false);
        IsModified = Value != def;
    }
}

public class IntSettingItem : SettingItemViewModel
{
    private string _text = "";
    public string Text
    {
        get => _text;
        set { if (SetProperty(ref _text, value)) UpdateIsModified(); }
    }

    public IntSettingItem(SettingDefinition def, Func<object> defaultFactory) : base(def, defaultFactory) { }

    public override void LoadFrom(object data)
    {
        var val = (int)(GetProperty(data, Definition.PropertyName) ?? 0);
        Text = val.ToString();
    }

    public override void ApplyTo(object data)
    {
        if (int.TryParse(Text, out var val))
        {
            if (Definition.MinInt.HasValue && Definition.MaxInt.HasValue)
                val = Math.Clamp(val, Definition.MinInt.Value, Definition.MaxInt.Value);
            SetPropertyValue(data, Definition.PropertyName, val);
        }
    }

    public override void UpdateIsModified()
    {
        var def = (int)(GetProperty(CreateDefault(), Definition.PropertyName) ?? 0);
        IsModified = Text != def.ToString();
    }
}

public class DoubleSettingItem : SettingItemViewModel
{
    private string _text = "";
    public string Text
    {
        get => _text;
        set { if (SetProperty(ref _text, value)) UpdateIsModified(); }
    }

    public DoubleSettingItem(SettingDefinition def, Func<object> defaultFactory) : base(def, defaultFactory) { }

    public override void LoadFrom(object data)
    {
        var val = (double)(GetProperty(data, Definition.PropertyName) ?? 0.0);
        Text = Definition.FormatString is not null
            ? val.ToString(Definition.FormatString)
            : val.ToString();
    }

    public override void ApplyTo(object data)
    {
        if (double.TryParse(Text, out var val))
        {
            if (Definition.MinDouble.HasValue && Definition.MaxDouble.HasValue)
                val = Math.Clamp(val, Definition.MinDouble.Value, Definition.MaxDouble.Value);
            SetPropertyValue(data, Definition.PropertyName, val);
        }
    }

    public override void UpdateIsModified()
    {
        var defaults = CreateDefault();
        var def = (double)(GetProperty(defaults, Definition.PropertyName) ?? 0.0);
        var defText = Definition.FormatString is not null
            ? def.ToString(Definition.FormatString)
            : def.ToString();
        IsModified = Text != defText;
    }
}

public class StringSettingItem : SettingItemViewModel
{
    private string _text = "";
    public string Text
    {
        get => _text;
        set { if (SetProperty(ref _text, value)) UpdateIsModified(); }
    }

    public StringSettingItem(SettingDefinition def, Func<object> defaultFactory) : base(def, defaultFactory) { }

    public override void LoadFrom(object data)
    {
        Text = (string)(GetProperty(data, Definition.PropertyName) ?? "");
    }

    public override void ApplyTo(object data)
    {
        SetPropertyValue(data, Definition.PropertyName, Text.Trim());
    }

    public override void UpdateIsModified()
    {
        var def = (string)(GetProperty(CreateDefault(), Definition.PropertyName) ?? "");
        IsModified = Text != def;
    }
}

public class TimeOnlySettingItem : SettingItemViewModel
{
    private string _text = "";
    public string Text
    {
        get => _text;
        set { if (SetProperty(ref _text, value)) UpdateIsModified(); }
    }

    public TimeOnlySettingItem(SettingDefinition def, Func<object> defaultFactory) : base(def, defaultFactory) { }

    public override void LoadFrom(object data)
    {
        var val = (TimeOnly)(GetProperty(data, Definition.PropertyName) ?? new TimeOnly());
        Text = val.ToString("HH:mm");
    }

    public override void ApplyTo(object data)
    {
        if (TimeOnly.TryParse(Text, out var val))
            SetPropertyValue(data, Definition.PropertyName, val);
    }

    public override void UpdateIsModified()
    {
        var def = (TimeOnly)(GetProperty(CreateDefault(), Definition.PropertyName) ?? new TimeOnly());
        IsModified = Text != def.ToString("HH:mm");
    }
}

public class ChoiceSettingItem : SettingItemViewModel
{
    private string _selectedValue = "";
    public string SelectedValue
    {
        get => _selectedValue;
        set { if (SetProperty(ref _selectedValue, value)) UpdateIsModified(); }
    }

    public string[] Choices { get; }

    public ChoiceSettingItem(SettingDefinition def, Func<object> defaultFactory) : base(def, defaultFactory)
    {
        Choices = def.Choices ?? [];
    }

    public override void LoadFrom(object data)
    {
        SelectedValue = GetProperty(data, Definition.PropertyName)?.ToString() ?? "";
    }

    public override void ApplyTo(object data)
    {
        var prop = GetPropertyInfo(data, Definition.PropertyName);
        if (prop?.PropertyType.IsEnum == true)
            SetPropertyValue(data, Definition.PropertyName, Enum.Parse(prop.PropertyType, SelectedValue));
        else
            SetPropertyValue(data, Definition.PropertyName, SelectedValue);
    }

    public override void UpdateIsModified()
    {
        var def = GetProperty(CreateDefault(), Definition.PropertyName)?.ToString() ?? "";
        IsModified = SelectedValue != def;
    }
}

public class IntListSettingItem : SettingItemViewModel
{
    private string _text = "";
    public string Text
    {
        get => _text;
        set { if (SetProperty(ref _text, value)) UpdateIsModified(); }
    }

    public IntListSettingItem(SettingDefinition def, Func<object> defaultFactory) : base(def, defaultFactory) { }

    public override void LoadFrom(object data)
    {
        var list = (List<int>?)GetProperty(data, Definition.PropertyName) ?? [];
        Text = string.Join(", ", list);
    }

    public override void ApplyTo(object data)
    {
        var parts = Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<int>();
        foreach (var part in parts)
            if (int.TryParse(part, out var p) && p > 0) result.Add(p);
        if (result.Count > 0)
            SetPropertyValue(data, Definition.PropertyName, result);
    }

    public override void UpdateIsModified()
    {
        var def = (List<int>?)GetProperty(CreateDefault(), Definition.PropertyName) ?? [];
        IsModified = Text != string.Join(", ", def);
    }
}

/// <summary>
/// Compound setting for value + mode (e.g., $/%/ticks).
/// </summary>
public class OffsetSettingItem : SettingItemViewModel
{
    private readonly string _modePropertyName;

    private string _text = "";
    public string Text
    {
        get => _text;
        set { if (SetProperty(ref _text, value)) UpdateIsModified(); }
    }

    private int _modeIndex;
    public int ModeIndex
    {
        get => _modeIndex;
        set { if (SetProperty(ref _modeIndex, value)) UpdateIsModified(); }
    }

    public string[] ModeChoices { get; }

    public OffsetSettingItem(SettingDefinition def, string modePropertyName, Func<object> defaultFactory) : base(def, defaultFactory)
    {
        _modePropertyName = modePropertyName;
        ModeChoices = def.Choices ?? ["$", "%", "ticks"];
    }

    public override void LoadFrom(object data)
    {
        var val = (double)(GetProperty(data, Definition.PropertyName) ?? 0.0);
        Text = val.ToString(Definition.FormatString ?? "0.###");
        ModeIndex = Convert.ToInt32(GetProperty(data, _modePropertyName) ?? 0);
        UpdateIsModified();
    }

    public override void ApplyTo(object data)
    {
        if (double.TryParse(Text, out var val))
            SetPropertyValue(data, Definition.PropertyName, val);
        SetPropertyValue(data, _modePropertyName, ModeIndex);
    }

    public override void UpdateIsModified()
    {
        var d = CreateDefault();
        var defVal = (double)(GetProperty(d, Definition.PropertyName) ?? 0.0);
        var defText = defVal.ToString(Definition.FormatString ?? "0.###");
        var defMode = Convert.ToInt32(GetProperty(d, _modePropertyName) ?? 0);
        IsModified = Text != defText || ModeIndex != defMode;
    }
}

/// <summary>
/// Compound setting for two TimeOnly values displayed side by side on one row.
/// </summary>
public class TimePairSettingItem : SettingItemViewModel
{
    private readonly SettingDefinition _rightDef;

    public string LeftDisplayName => Definition.DisplayName;
    public string RightDisplayName => _rightDef.DisplayName;
    public string LeftKey => Definition.Key;
    public string RightKey => _rightDef.Key;
    public string LeftDescription => Definition.Description;
    public string RightDescription => _rightDef.Description;

    private string _leftText = "";
    public string LeftText
    {
        get => _leftText;
        set { if (SetProperty(ref _leftText, value)) UpdateIsModified(); }
    }

    private string _rightText = "";
    public string RightText
    {
        get => _rightText;
        set { if (SetProperty(ref _rightText, value)) UpdateIsModified(); }
    }

    public TimePairSettingItem(SettingDefinition left, SettingDefinition right, Func<object> defaultFactory) : base(left, defaultFactory)
    {
        _rightDef = right;
    }

    public override void LoadFrom(object data)
    {
        var leftVal = (TimeOnly)(GetProperty(data, Definition.PropertyName) ?? new TimeOnly());
        LeftText = leftVal.ToString("HH:mm");
        var rightVal = (TimeOnly)(GetProperty(data, _rightDef.PropertyName) ?? new TimeOnly());
        RightText = rightVal.ToString("HH:mm");
    }

    public override void ApplyTo(object data)
    {
        if (TimeOnly.TryParse(LeftText, out var leftVal))
            SetPropertyValue(data, Definition.PropertyName, leftVal);
        if (TimeOnly.TryParse(RightText, out var rightVal))
            SetPropertyValue(data, _rightDef.PropertyName, rightVal);
    }

    public override void UpdateIsModified()
    {
        var defaults = CreateDefault();
        var defLeft = (TimeOnly)(GetProperty(defaults, Definition.PropertyName) ?? new TimeOnly());
        var defRight = (TimeOnly)(GetProperty(defaults, _rightDef.PropertyName) ?? new TimeOnly());
        IsModified = LeftText != defLeft.ToString("HH:mm") || RightText != defRight.ToString("HH:mm");
    }

    public override bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Key.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.Key.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Compound setting for two int values displayed side by side on one row.
/// </summary>
public class IntPairSettingItem : SettingItemViewModel
{
    private readonly SettingDefinition _rightDef;

    public string LeftDisplayName => Definition.DisplayName;
    public string RightDisplayName => _rightDef.DisplayName;
    public string LeftKey => Definition.Key;
    public string RightKey => _rightDef.Key;
    public string LeftDescription => Definition.Description;
    public string RightDescription => _rightDef.Description;

    private string _leftText = "";
    public string LeftText
    {
        get => _leftText;
        set { if (SetProperty(ref _leftText, value)) UpdateIsModified(); }
    }

    private string _rightText = "";
    public string RightText
    {
        get => _rightText;
        set { if (SetProperty(ref _rightText, value)) UpdateIsModified(); }
    }

    public IntPairSettingItem(SettingDefinition left, SettingDefinition right, Func<object> defaultFactory) : base(left, defaultFactory)
    {
        _rightDef = right;
    }

    public override void LoadFrom(object data)
    {
        var leftVal = (int)(GetProperty(data, Definition.PropertyName) ?? 0);
        LeftText = leftVal.ToString();
        var rightVal = (int)(GetProperty(data, _rightDef.PropertyName) ?? 0);
        RightText = rightVal.ToString();
    }

    public override void ApplyTo(object data)
    {
        if (int.TryParse(LeftText, out var leftVal))
        {
            if (Definition.MinInt.HasValue && Definition.MaxInt.HasValue)
                leftVal = Math.Clamp(leftVal, Definition.MinInt.Value, Definition.MaxInt.Value);
            SetPropertyValue(data, Definition.PropertyName, leftVal);
        }
        if (int.TryParse(RightText, out var rightVal))
        {
            if (_rightDef.MinInt.HasValue && _rightDef.MaxInt.HasValue)
                rightVal = Math.Clamp(rightVal, _rightDef.MinInt.Value, _rightDef.MaxInt.Value);
            SetPropertyValue(data, _rightDef.PropertyName, rightVal);
        }
    }

    public override void UpdateIsModified()
    {
        var defaults = CreateDefault();
        var defLeft = (int)(GetProperty(defaults, Definition.PropertyName) ?? 0);
        var defRight = (int)(GetProperty(defaults, _rightDef.PropertyName) ?? 0);
        IsModified = LeftText != defLeft.ToString() || RightText != defRight.ToString();
    }

    public override bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Key.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.Key.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Compound setting for two double values displayed side by side on one row.
/// </summary>
public class DoublePairSettingItem : SettingItemViewModel
{
    private readonly SettingDefinition _rightDef;

    public string LeftDisplayName => Definition.DisplayName;
    public string RightDisplayName => _rightDef.DisplayName;
    public string LeftKey => Definition.Key;
    public string RightKey => _rightDef.Key;
    public string LeftDescription => Definition.Description;
    public string RightDescription => _rightDef.Description;

    private string _leftText = "";
    public string LeftText
    {
        get => _leftText;
        set { if (SetProperty(ref _leftText, value)) UpdateIsModified(); }
    }

    private string _rightText = "";
    public string RightText
    {
        get => _rightText;
        set { if (SetProperty(ref _rightText, value)) UpdateIsModified(); }
    }

    public DoublePairSettingItem(SettingDefinition left, SettingDefinition right, Func<object> defaultFactory) : base(left, defaultFactory)
    {
        _rightDef = right;
    }

    public override void LoadFrom(object data)
    {
        var leftVal = Convert.ToDouble(GetProperty(data, Definition.PropertyName) ?? 0.0);
        LeftText = Definition.FormatString is not null
            ? leftVal.ToString(Definition.FormatString)
            : leftVal.ToString();
        var rightVal = Convert.ToDouble(GetProperty(data, _rightDef.PropertyName) ?? 0.0);
        RightText = _rightDef.FormatString is not null
            ? rightVal.ToString(_rightDef.FormatString)
            : rightVal.ToString();
    }

    public override void ApplyTo(object data)
    {
        if (double.TryParse(LeftText, out var leftVal))
        {
            if (Definition.MinDouble.HasValue && Definition.MaxDouble.HasValue)
                leftVal = Math.Clamp(leftVal, Definition.MinDouble.Value, Definition.MaxDouble.Value);
            SetPropertyValue(data, Definition.PropertyName, leftVal);
        }
        if (double.TryParse(RightText, out var rightVal))
        {
            if (_rightDef.MinDouble.HasValue && _rightDef.MaxDouble.HasValue)
                rightVal = Math.Clamp(rightVal, _rightDef.MinDouble.Value, _rightDef.MaxDouble.Value);
            SetPropertyValue(data, _rightDef.PropertyName, rightVal);
        }
    }

    public override void UpdateIsModified()
    {
        var defaults = CreateDefault();
        var defLeft = Convert.ToDouble(GetProperty(defaults, Definition.PropertyName) ?? 0.0);
        var defLeftText = Definition.FormatString is not null
            ? defLeft.ToString(Definition.FormatString)
            : defLeft.ToString();
        var defRight = Convert.ToDouble(GetProperty(defaults, _rightDef.PropertyName) ?? 0.0);
        var defRightText = _rightDef.FormatString is not null
            ? defRight.ToString(_rightDef.FormatString)
            : defRight.ToString();
        IsModified = LeftText != defLeftText || RightText != defRightText;
    }

    public override bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Key.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _rightDef.Key.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Lightweight marker for category headers in the flat list (not a setting).
/// </summary>
public class CategoryHeaderItem : ViewModelBase
{
    public string Name { get; }
    public CategoryHeaderItem(string name) => Name = name;
}
