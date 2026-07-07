using System.Windows;
using System.Windows.Media;

namespace Raisin.StyleInspector;

public class StyleChainEntry
{
    public required string Label { get; init; }
    public required string Detail { get; init; }
    public Style? Style { get; init; }
    public int SetterCount { get; init; }
    public int TriggerCount { get; init; }
    public string? DictionarySource { get; init; }
    public string? DictionaryLevel { get; init; }
}

public class StyleSetterInfo
{
    public required string StyleLabel { get; init; }
    public required string DisplayValue { get; init; }
    public string? DictionarySource { get; init; }
    public string? DictionaryLevel { get; init; }
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
            var (dictSource, dictLevel) = FindDictionaryInfo(element, current);

            chain.Add(new StyleChainEntry
            {
                Label = label,
                Detail = $"TargetType={targetType}, {current.Setters.Count} setter(s), {current.Triggers.Count} trigger(s)",
                Style = current,
                SetterCount = current.Setters.Count,
                TriggerCount = current.Triggers.Count,
                DictionarySource = dictSource,
                DictionaryLevel = dictLevel,
            });

            current = current.BasedOn;
            depth++;
        }

        return chain;
    }

    public static Dictionary<string, List<StyleSetterInfo>> ResolveSetterOrigins(FrameworkElement element)
    {
        var result = new Dictionary<string, List<StyleSetterInfo>>(StringComparer.Ordinal);
        var chain = Resolve(element);

        foreach (var entry in chain)
        {
            if (entry.Style == null) continue;
            foreach (var setter in entry.Style.Setters.OfType<Setter>())
            {
                if (setter.Property == null) continue;
                var name = setter.Property.Name;
                if (!result.TryGetValue(name, out var list))
                {
                    list = new List<StyleSetterInfo>();
                    result[name] = list;
                }
                list.Add(new StyleSetterInfo
                {
                    StyleLabel = entry.Label,
                    DisplayValue = PropertyEnumerator.FormatValue(setter.Value, null),
                    DictionarySource = entry.DictionarySource,
                    DictionaryLevel = entry.DictionaryLevel,
                });
            }
        }

        return result;
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

    internal static string? FindDictionarySource(DependencyObject element, Style style)
    {
        var info = FindDictionaryInfo(element, style);
        return info.source;
    }

    internal static string? FindDictionaryLevel(DependencyObject element, Style style)
    {
        var info = FindDictionaryInfo(element, style);
        return info.level;
    }

    internal static (string? source, string? level) FindDictionaryInfo(DependencyObject element, Style style)
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.Resources.Count > 0)
            {
                if (SearchDictForStyle(fe.Resources, style, out var uri))
                {
                    if (uri == null)
                        return (fe.GetType().Name, "Inline");
                    var (display, level) = FormatDictionaryUri(uri);
                    return (display, level);
                }
            }
            try { current = VisualTreeHelper.GetParent(current); }
            catch { break; }
        }

        try
        {
            if (Application.Current?.Resources is { } appRes &&
                SearchDictForStyle(appRes, style, out var appUri))
            {
                if (appUri == null)
                    return ("Application", "App");
                var (display, level) = FormatDictionaryUri(appUri);
                return (display, level);
            }
        }
        catch { }

        return (null, null);
    }

    private static bool SearchDictForStyle(ResourceDictionary dict, Style style, out Uri? sourceUri, int depth = 0)
    {
        sourceUri = null;
        if (depth > 8) return false;

        foreach (var key in dict.Keys)
        {
            try
            {
                if (ReferenceEquals(dict[key], style))
                {
                    sourceUri = dict.Source;
                    return true;
                }
            }
            catch { }
        }

        foreach (var merged in dict.MergedDictionaries)
        {
            if (SearchDictForStyle(merged, style, out sourceUri, depth + 1))
                return true;
        }

        return false;
    }

    private static readonly string? AppAssemblyName =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;

    private static (string display, string level) FormatDictionaryUri(Uri uri)
    {
        var (assembly, fileName) = ParsePackUri(uri);
        var level = ClassifyAssembly(assembly);

        string display;
        if (assembly == null || string.Equals(assembly, AppAssemblyName, StringComparison.OrdinalIgnoreCase))
            display = fileName;
        else
            display = $"{assembly}: {fileName}";

        return (display, level);
    }

    private static (string? assembly, string fileName) ParsePackUri(Uri uri)
    {
        var path = uri.OriginalString;

        var componentIdx = path.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
        string? assembly = null;
        string filePart;

        if (componentIdx >= 0)
        {
            var beforeComponent = path[..componentIdx];
            var lastSlash = beforeComponent.LastIndexOf('/');
            assembly = lastSlash >= 0 ? beforeComponent[(lastSlash + 1)..] : beforeComponent;
            filePart = path[(componentIdx + ";component/".Length)..];
        }
        else
        {
            filePart = path;
        }

        var lastFileSlash = filePart.LastIndexOf('/');
        var name = lastFileSlash >= 0 ? filePart[(lastFileSlash + 1)..] : filePart;
        if (name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];

        return (assembly, name);
    }

    private static string ClassifyAssembly(string? assembly)
    {
        if (assembly == null || string.Equals(assembly, AppAssemblyName, StringComparison.OrdinalIgnoreCase))
            return "Project";

        if (assembly.Contains("MaterialDesign", StringComparison.OrdinalIgnoreCase) ||
            assembly.Contains("Theme", StringComparison.OrdinalIgnoreCase))
            return "Theme";

        return "Library";
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
