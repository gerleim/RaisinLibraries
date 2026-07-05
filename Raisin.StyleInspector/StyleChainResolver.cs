using System.Windows;

namespace Raisin.StyleInspector;

public class StyleChainEntry
{
    public required string Label { get; init; }
    public required string Detail { get; init; }
    public Style? Style { get; init; }
    public int SetterCount { get; init; }
    public int TriggerCount { get; init; }
}

internal static class StyleChainResolver
{
    public static List<StyleChainEntry> Resolve(FrameworkElement element)
    {
        var chain = new List<StyleChainEntry>();

        var style = element.Style;
        if (style == null)
        {
            chain.Add(new StyleChainEntry
            {
                Label = $"[instance] {element.GetType().Name}",
                Detail = "No style applied",
                SetterCount = 0,
                TriggerCount = 0,
            });
            return chain;
        }

        var localCount = CountLocalValues(element);
        if (localCount > 0)
        {
            chain.Add(new StyleChainEntry
            {
                Label = "[instance]",
                Detail = $"{localCount} local value(s)",
                SetterCount = localCount,
                TriggerCount = 0,
            });
        }

        var depth = 0;
        var current = style;
        while (current != null && depth < 20)
        {
            var key = TryGetStyleKey(element, current, depth);
            var label = key ?? $"Style (depth {depth})";
            var targetType = current.TargetType?.Name ?? "?";

            chain.Add(new StyleChainEntry
            {
                Label = label,
                Detail = $"TargetType={targetType}, {current.Setters.Count} setter(s), {current.Triggers.Count} trigger(s)",
                Style = current,
                SetterCount = current.Setters.Count,
                TriggerCount = current.Triggers.Count,
            });

            current = current.BasedOn;
            depth++;
        }

        return chain;
    }

    public static List<string> GetSetterNames(Style style)
    {
        var names = new List<string>();
        foreach (var setter in style.Setters.OfType<Setter>())
        {
            if (setter.Property != null)
                names.Add(setter.Property.Name);
        }
        return names;
    }

    private static int CountLocalValues(DependencyObject element)
    {
        var count = 0;
        var enumerator = element.GetLocalValueEnumerator();
        while (enumerator.MoveNext())
        {
            var entry = enumerator.Current;
            if (entry.Value != DependencyProperty.UnsetValue &&
                !entry.Property.ReadOnly)
                count++;
        }
        return count;
    }

    private static string? TryGetStyleKey(FrameworkElement element, Style style, int depth)
    {
        if (depth == 0)
        {
            var localStyle = element.ReadLocalValue(FrameworkElement.StyleProperty);
            if (localStyle != DependencyProperty.UnsetValue && localStyle == style)
                return "[local Style]";
        }

        if (style.TargetType != null)
        {
            var key = style.TargetType.Name;
            if (style.BasedOn != null)
                return $"{key} (BasedOn)";
            return key;
        }

        return null;
    }
}
