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

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public static List<InspectedProperty> Enumerate(DependencyObject element)
    {
        var results = new List<InspectedProperty>();
        var seen = new HashSet<DependencyProperty>();

        var fields = element.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.FieldType == typeof(DependencyProperty));

        foreach (var field in fields)
        {
            var dp = (DependencyProperty)field.GetValue(null)!;
            if (!seen.Add(dp)) continue;
            try { AddProperty(results, element, dp, field.DeclaringType?.Name ?? "Other"); }
            catch { /* skip unreadable properties */ }
        }

        var localEnum = element.GetLocalValueEnumerator();
        while (localEnum.MoveNext())
        {
            var entry = localEnum.Current;
            if (!seen.Add(entry.Property)) continue;
            try { AddProperty(results, element, entry.Property, entry.Property.OwnerType.Name); }
            catch { }
        }

        return results.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
    }

    private static void AddProperty(List<InspectedProperty> results, DependencyObject element,
        DependencyProperty dp, string category)
    {
        var value = element.GetValue(dp);
        var source = DependencyPropertyHelper.GetValueSource(element, dp);
        var resourceKey = TryGetResourceKey(element, dp);

        results.Add(new InspectedProperty
        {
            Name = dp.Name,
            Category = category,
            Value = value,
            DisplayValue = FormatValue(value, resourceKey),
            Source = source.BaseValueSource,
            SourceTag = GetSourceTag(source.BaseValueSource),
            SourceBrush = GetSourceBrush(source.BaseValueSource),
            ColorPreview = GetColorPreview(value),
            ResourceKey = resourceKey,
            Property = dp,
        });
    }

    private static string GetSourceTag(BaseValueSource source) => source switch
    {
        BaseValueSource.Default => "D",
        BaseValueSource.Inherited => "I",
        BaseValueSource.Style => "S",
        BaseValueSource.StyleTrigger => "ST",
        BaseValueSource.ParentTemplate => "T",
        BaseValueSource.ParentTemplateTrigger => "TT",
        BaseValueSource.TemplateTrigger => "TT",
        BaseValueSource.Local => "L",
        _ => "?",
    };

    private static Brush GetSourceBrush(BaseValueSource source) => source switch
    {
        BaseValueSource.Default => DefaultBrush,
        BaseValueSource.Inherited => InheritedBrush,
        BaseValueSource.Style or BaseValueSource.StyleTrigger => StyleBrush,
        BaseValueSource.ParentTemplate or BaseValueSource.ParentTemplateTrigger
            or BaseValueSource.TemplateTrigger => TemplateBrush,
        BaseValueSource.Local => LocalBrush,
        _ => DefaultBrush,
    };

    private static string FormatValue(object? value, string? resourceKey)
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

    private static Brush? GetColorPreview(object? value)
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
}
