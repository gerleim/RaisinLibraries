using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace Raisin.StyleInspector;

internal static class PropertyEnumerator
{
    private static readonly Brush DefaultBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)));
    private static readonly Brush StyleBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)));
    private static readonly Brush TemplateBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)));
    private static readonly Brush InheritedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)));
    private static readonly Brush LocalBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78)));
    private static readonly Brush ThemeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public static List<InspectedProperty> Enumerate(DependencyObject element)
    {
        var results = new List<InspectedProperty>();
        var seen = new HashSet<DependencyProperty>();
        var resourceIndex = BuildResourceIndex(element);

        var fields = element.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.FieldType == typeof(DependencyProperty));

        foreach (var field in fields)
        {
            var dp = (DependencyProperty)field.GetValue(null)!;
            if (!seen.Add(dp)) continue;
            try { AddProperty(results, element, dp, field.DeclaringType?.Name ?? "Other", resourceIndex); }
            catch { }
        }

        var localEnum = element.GetLocalValueEnumerator();
        while (localEnum.MoveNext())
        {
            var entry = localEnum.Current;
            if (!seen.Add(entry.Property)) continue;
            try { AddProperty(results, element, entry.Property, entry.Property.OwnerType.Name, resourceIndex); }
            catch { }
        }

        return results.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
    }

    private static void AddProperty(List<InspectedProperty> results, DependencyObject element,
        DependencyProperty dp, string category, Dictionary<object, string> resourceIndex)
    {
        var value = element.GetValue(dp);
        var source = DependencyPropertyHelper.GetValueSource(element, dp);
        var resourceKey = TryGetResourceKey(element, dp)
            ?? LookupResourceKey(value, resourceIndex);

        results.Add(new InspectedProperty
        {
            Name = dp.Name,
            Category = category,
            Value = value,
            DisplayValue = FormatValue(value, resourceKey),
            Source = source.BaseValueSource,
            SourceTag = GetSourceTag(source.BaseValueSource),
            SourceTooltip = GetSourceTooltip(source.BaseValueSource),
            SourceBrush = GetSourceBrush(source.BaseValueSource),
            ColorPreview = GetColorPreview(value),
            ResourceKey = resourceKey,
            Property = dp,
            Element = element,
        });
    }

    internal static string GetSourceTooltip(BaseValueSource source) => source switch
    {
        BaseValueSource.Default => "Default — dependency property default value",
        BaseValueSource.Inherited => "Inherited — inherited from a parent element",
        BaseValueSource.DefaultStyle => "Theme — value from theme/generic style",
        BaseValueSource.ImplicitStyleReference => "Implicit Style — value from a keyless style matching the element type",
        BaseValueSource.Style => "Style — value set by an explicit style",
        BaseValueSource.StyleTrigger => "Style Trigger — value set by a trigger in a style",
        BaseValueSource.ParentTemplate => "Template — value from the parent's control template",
        BaseValueSource.ParentTemplateTrigger => "Template Trigger — value set by a trigger in the parent template",
        BaseValueSource.TemplateTrigger => "Template Trigger — value set by a trigger in a template",
        BaseValueSource.Local => "Local — value set directly on the element instance",
        _ => "Unknown value source",
    };

    internal static string GetSourceTag(BaseValueSource source) => source switch
    {
        BaseValueSource.Default => "D",
        BaseValueSource.Inherited => "I",
        BaseValueSource.DefaultStyle => "Th",
        BaseValueSource.ImplicitStyleReference => "IS",
        BaseValueSource.Style => "S",
        BaseValueSource.StyleTrigger => "ST",
        BaseValueSource.ParentTemplate => "T",
        BaseValueSource.ParentTemplateTrigger => "TT",
        BaseValueSource.TemplateTrigger => "TT",
        BaseValueSource.Local => "L",
        _ => "?",
    };

    internal static Brush GetSourceBrush(BaseValueSource source) => source switch
    {
        BaseValueSource.Default => DefaultBrush,
        BaseValueSource.Inherited => InheritedBrush,
        BaseValueSource.DefaultStyle => ThemeBrush,
        BaseValueSource.ImplicitStyleReference or BaseValueSource.Style
            or BaseValueSource.StyleTrigger => StyleBrush,
        BaseValueSource.ParentTemplate or BaseValueSource.ParentTemplateTrigger
            or BaseValueSource.TemplateTrigger => TemplateBrush,
        BaseValueSource.Local => LocalBrush,
        _ => DefaultBrush,
    };

    internal static string FormatValue(object? value, string? resourceKey)
    {
        var formatted = value switch
        {
            null => "(null)",
            SolidColorBrush brush => brush.Color.ToString(),
            LinearGradientBrush => "(LinearGradient)",
            RadialGradientBrush => "(RadialGradient)",
            Color color => color.ToString(),
            Thickness t => $"{t.Left},{t.Top},{t.Right},{t.Bottom}",
            CornerRadius cr => $"{cr.TopLeft},{cr.TopRight},{cr.BottomRight},{cr.BottomLeft}",
            GridLength gl => gl.IsAuto ? "Auto" : gl.IsStar ? $"{gl.Value}*" : gl.Value.ToString("G"),
            double d when double.IsNaN(d) => "Auto",
            double d when double.IsPositiveInfinity(d) => "∞",
            double d => d.ToString("G"),
            bool b => b.ToString(),
            Enum e => e.ToString(),
            FontFamily ff => ff.Source,
            string s => s.Length > 80 ? s[..80] + "…" : s,
            _ => value.ToString() ?? "(null)",
        };

        return resourceKey != null ? $"{{{resourceKey}}} → {formatted}" : formatted;
    }

    internal static Brush? GetColorPreview(object? value)
    {
        Color? color = value switch
        {
            SolidColorBrush brush => brush.Color,
            Color c => c,
            _ => null,
        };
        if (color == null) return null;
        var preview = new SolidColorBrush(color.Value);
        preview.Freeze();
        return preview;
    }

    internal static object? ParseValue(string text, Type targetType)
    {
        try
        {
            if (targetType == typeof(string)) return text;
            if (targetType == typeof(double))
            {
                if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return double.NaN;
                return double.TryParse(text, out var d) ? d : null;
            }
            if (targetType == typeof(int)) return int.TryParse(text, out var i) ? i : null;
            if (targetType == typeof(bool)) return bool.TryParse(text, out var b) ? b : null;
            if (targetType == typeof(Thickness))
            {
                var parts = text.Split(',').Select(s => double.TryParse(s.Trim(), out var v) ? v : 0.0).ToArray();
                return parts.Length switch
                {
                    1 => new Thickness(parts[0]),
                    2 => new Thickness(parts[0], parts[1], parts[0], parts[1]),
                    4 => new Thickness(parts[0], parts[1], parts[2], parts[3]),
                    _ => null,
                };
            }
            if (targetType == typeof(CornerRadius))
            {
                var parts = text.Split(',').Select(s => double.TryParse(s.Trim(), out var v) ? v : 0.0).ToArray();
                return parts.Length switch
                {
                    1 => new CornerRadius(parts[0]),
                    4 => new CornerRadius(parts[0], parts[1], parts[2], parts[3]),
                    _ => null,
                };
            }
            if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
                return new BrushConverter().ConvertFromString(text);
            if (targetType == typeof(Color))
                return ColorConverter.ConvertFromString(text);
            if (targetType == typeof(FontFamily))
                return new FontFamily(text);
            if (targetType == typeof(GridLength))
            {
                if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return GridLength.Auto;
                if (text.EndsWith('*'))
                {
                    var n = text[..^1];
                    return string.IsNullOrEmpty(n) ? new GridLength(1, GridUnitType.Star)
                        : double.TryParse(n, out var s) ? new GridLength(s, GridUnitType.Star) : null;
                }
                return double.TryParse(text, out var px) ? new GridLength(px) : null;
            }
            if (targetType.IsEnum)
                return Enum.Parse(targetType, text, ignoreCase: true);
        }
        catch { }
        return null;
    }

    private static string? TryGetResourceKey(DependencyObject element, DependencyProperty dp)
    {
        try
        {
            var localValue = element.ReadLocalValue(dp);
            if (localValue == DependencyProperty.UnsetValue) return null;

            var type = localValue.GetType();
            if (type.Name == "ResourceReferenceExpression")
            {
                var keyProp = type.GetProperty("ResourceKey",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return keyProp?.GetValue(localValue)?.ToString();
            }
        }
        catch { }
        return null;
    }

    private static string? LookupResourceKey(object? value, Dictionary<object, string> index)
    {
        if (value == null || value is string || value is ValueType) return null;
        return index.TryGetValue(value, out var key) ? key : null;
    }

    private static Dictionary<object, string> BuildResourceIndex(DependencyObject element)
    {
        var index = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);

        DependencyObject? current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.Resources.Count > 0)
                IndexDictionary(fe.Resources, index);
            try { current = VisualTreeHelper.GetParent(current); }
            catch { break; }
        }

        try
        {
            if (Application.Current?.Resources is { } appRes)
                IndexDictionary(appRes, index);
        }
        catch { }

        return index;
    }

    private static void IndexDictionary(ResourceDictionary dict, Dictionary<object, string> index, int depth = 0)
    {
        if (depth > 8) return;

        foreach (var key in dict.Keys)
        {
            try
            {
                var val = dict[key];
                if (val != null && key is string strKey && !index.ContainsKey(val))
                    index[val] = strKey;
            }
            catch { }
        }

        foreach (var merged in dict.MergedDictionaries)
            IndexDictionary(merged, index, depth + 1);
    }
}
