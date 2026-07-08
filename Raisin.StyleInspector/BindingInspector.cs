using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Raisin.StyleInspector;

internal static class BindingInspector
{
    private static readonly Brush ActiveBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)));
    private static readonly Brush ErrorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF4, 0x48, 0x47)));
    private static readonly Brush InactiveBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public static List<BindingInfo> ResolveBindings(DependencyObject element)
    {
        var results = new List<BindingInfo>();
        var seen = new HashSet<DependencyProperty>();

        var fields = element.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.FieldType == typeof(DependencyProperty));

        foreach (var field in fields)
        {
            var dp = (DependencyProperty)field.GetValue(null)!;
            if (!seen.Add(dp)) continue;
            TryAdd(results, element, dp);
        }

        var localEnum = element.GetLocalValueEnumerator();
        while (localEnum.MoveNext())
        {
            if (!seen.Add(localEnum.Current.Property)) continue;
            TryAdd(results, element, localEnum.Current.Property);
        }

        return results.OrderBy(b => b.PropertyName).ToList();
    }

    private static void TryAdd(List<BindingInfo> results, DependencyObject element, DependencyProperty dp)
    {
        try
        {
            var be = BindingOperations.GetBindingExpression(element, dp);
            if (be != null)
            {
                results.Add(BuildInfo(dp, be));
                return;
            }

            var mbe = BindingOperations.GetMultiBindingExpression(element, dp);
            if (mbe != null)
                results.Add(BuildMultiInfo(dp, mbe));
        }
        catch { }
    }

    private static BindingInfo BuildInfo(DependencyProperty dp, BindingExpression be)
    {
        var binding = be.ParentBinding;
        var path = binding.Path?.Path;
        if (string.IsNullOrEmpty(path)) path = ".";
        var source = DescribeSource(binding, be);
        var mode = binding.Mode == BindingMode.Default ? null : binding.Mode.ToString();
        var converter = binding.Converter?.GetType().Name;
        var currentValue = FormatCurrentValue(be);
        var (indicator, brush, hasError) = ClassifyStatus(be);

        var tooltipParts = new List<string>
        {
            $"Property: {dp.Name}",
            $"Path: {path}",
            $"Source: {source}",
        };
        if (mode != null) tooltipParts.Add($"Mode: {mode}");
        if (converter != null) tooltipParts.Add($"Converter: {converter}");
        if (binding.StringFormat != null) tooltipParts.Add($"StringFormat: {binding.StringFormat}");
        try
        {
            if (binding.FallbackValue != DependencyProperty.UnsetValue)
                tooltipParts.Add($"FallbackValue: {binding.FallbackValue}");
        }
        catch { }
        try
        {
            if (binding.TargetNullValue != DependencyProperty.UnsetValue)
                tooltipParts.Add($"TargetNullValue: {binding.TargetNullValue}");
        }
        catch { }
        tooltipParts.Add($"Status: {be.Status}");
        if (currentValue != null) tooltipParts.Add($"Current: {currentValue}");
        if (hasError) tooltipParts.Add($"Error: {GetErrorMessage(be)}");

        return new BindingInfo
        {
            PropertyName = dp.Name,
            Path = path,
            SourceDescription = source,
            Mode = mode,
            Converter = converter,
            CurrentValue = currentValue,
            StatusIndicator = indicator,
            StatusBrush = brush,
            HasError = hasError,
            Tooltip = string.Join("\n", tooltipParts),
        };
    }

    private static BindingInfo BuildMultiInfo(DependencyProperty dp, MultiBindingExpression mbe)
    {
        var mb = mbe.ParentMultiBinding;
        var childPaths = mbe.BindingExpressions
            .OfType<BindingExpression>()
            .Select(be => be.ParentBinding.Path?.Path ?? "?");
        var path = $"Multi({string.Join(", ", childPaths)})";
        var converter = mb.Converter?.GetType().Name ?? "?";
        var mode = mb.Mode == BindingMode.Default ? null : mb.Mode.ToString();
        var currentValue = FormatCurrentValue(mbe);
        var (indicator, brush, hasError) = ClassifyStatus(mbe);

        return new BindingInfo
        {
            PropertyName = dp.Name,
            Path = path,
            SourceDescription = "MultiBinding",
            Mode = mode,
            Converter = converter,
            CurrentValue = currentValue,
            StatusIndicator = indicator,
            StatusBrush = brush,
            HasError = hasError,
            Tooltip = $"MultiBinding with {mbe.BindingExpressions.Count} bindings\nConverter: {converter}\nStatus: {mbe.Status}",
        };
    }

    private static string DescribeSource(Binding binding, BindingExpression be)
    {
        if (binding.Source != null)
            return binding.Source.GetType().Name;

        if (binding.RelativeSource is { } rs)
        {
            return rs.Mode switch
            {
                RelativeSourceMode.Self => "Self",
                RelativeSourceMode.TemplatedParent => "TemplatedParent",
                RelativeSourceMode.FindAncestor => $"Ancestor({rs.AncestorType?.Name ?? "?"})",
                RelativeSourceMode.PreviousData => "PreviousData",
                _ => "RelativeSource",
            };
        }

        if (!string.IsNullOrEmpty(binding.ElementName))
            return $"#{binding.ElementName}";

        try
        {
            var dataItem = be.DataItem;
            if (dataItem != null)
                return dataItem.GetType().Name;
        }
        catch { }

        return "DataContext";
    }

    private static string? FormatCurrentValue(BindingExpressionBase be)
    {
        try
        {
            var target = be.Target;
            var dp = be.TargetProperty;
            if (target == null || dp == null) return null;
            return PropertyEnumerator.FormatValue(target.GetValue(dp), null);
        }
        catch { return null; }
    }

    private static (string indicator, Brush brush, bool hasError) ClassifyStatus(BindingExpressionBase be)
    {
        try { if (be.HasError) return ("✗", ErrorBrush, true); }
        catch { }

        return be.Status switch
        {
            BindingStatus.Active => ("●", ActiveBrush, false),
            BindingStatus.Inactive => ("○", InactiveBrush, false),
            BindingStatus.Detached => ("○", InactiveBrush, false),
            BindingStatus.Unattached => ("◌", InactiveBrush, false),
            BindingStatus.PathError => ("✗", ErrorBrush, true),
            BindingStatus.UpdateSourceError => ("✗", ErrorBrush, true),
            _ => ("?", InactiveBrush, false),
        };
    }

    private static string GetErrorMessage(BindingExpressionBase be)
    {
        try
        {
            if (be.ValidationError?.ErrorContent is { } content)
                return content.ToString() ?? "Validation error";
        }
        catch { }

        return be.Status switch
        {
            BindingStatus.PathError => "Binding path could not be resolved",
            BindingStatus.UpdateSourceError => "Failed to update source",
            _ => "Binding error",
        };
    }
}
