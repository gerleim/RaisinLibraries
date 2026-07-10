using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Raisin.StyleInspector;

public class TemplateInfo
{
    public required string TargetType { get; init; }
    public string? DictionarySource { get; init; }
}

internal static class TemplateInspector
{
    public static TemplateInfo? ResolveTemplate(FrameworkElement element)
    {
        var template = (element as Control)?.Template;
        if (template == null) return null;

        return new TemplateInfo
        {
            TargetType = template.TargetType?.Name ?? "?",
            DictionarySource = FindTemplateSource(element, template),
        };
    }

    public static List<TemplateTriggerInfo> ResolveAllTriggers(FrameworkElement element)
    {
        var result = new List<TemplateTriggerInfo>();

        // Style triggers from the BasedOn chain
        var chain = StyleChainResolver.Resolve(element);
        foreach (var entry in chain)
        {
            if (entry.Style == null || entry.Style.Triggers.Count == 0) continue;
            foreach (var trigger in entry.Style.Triggers)
            {
                var info = ResolveTrigger(trigger, element, entry.Label);
                if (info != null) result.Add(info);
            }
        }

        // Template triggers
        var template = (element as Control)?.Template;
        if (template != null)
        {
            foreach (var trigger in template.Triggers)
            {
                var info = ResolveTrigger(trigger, element, "Template");
                if (info != null) result.Add(info);
            }
        }

        // Visual state groups
        result.AddRange(ResolveVisualStateGroups(element));

        return result;
    }

    private static TemplateTriggerInfo? ResolveTrigger(TriggerBase trigger, FrameworkElement element, string source)
    {
        switch (trigger)
        {
            case System.Windows.Trigger t:
            {
                var canCheck = string.IsNullOrEmpty(t.SourceName);
                return new TemplateTriggerInfo
                {
                    Source = source,
                    Condition = FormatCondition(t),
                    Setters = t.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(t.Setters),
                    IsActive = canCheck ? CheckActive(element, t.Property, t.Value) : null,
                    Evaluator = canCheck ? () => CheckActive(element, t.Property, t.Value) : null,
                };
            }

            case MultiTrigger mt:
            {
                var conditions = mt.Conditions.Select(c =>
                    string.IsNullOrEmpty(c.SourceName)
                        ? $"{c.Property.Name} = {FormatValue(c.Value)}"
                        : $"{c.SourceName}.{c.Property.Name} = {FormatValue(c.Value)}");
                var allOnSelf = mt.Conditions.All(c => string.IsNullOrEmpty(c.SourceName));
                return new TemplateTriggerInfo
                {
                    Source = source,
                    Condition = string.Join(" AND ", conditions),
                    Setters = mt.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(mt.Setters),
                    IsActive = allOnSelf ? CheckMultiActive(element, mt) : null,
                    Evaluator = allOnSelf ? () => CheckMultiActive(element, mt) : null,
                };
            }

            case DataTrigger dt:
                return new TemplateTriggerInfo
                {
                    Source = source,
                    Condition = $"[Binding] = {FormatValue(dt.Value)}",
                    Setters = dt.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(dt.Setters),
                };

            case MultiDataTrigger mdt:
            {
                var conditions = mdt.Conditions.Select(c => $"[Binding] = {FormatValue(c.Value)}");
                return new TemplateTriggerInfo
                {
                    Source = source,
                    Condition = string.Join(" AND ", conditions),
                    Setters = mdt.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(mdt.Setters),
                };
            }

            case EventTrigger et:
                return new TemplateTriggerInfo
                {
                    Source = source,
                    Condition = $"Event: {et.RoutedEvent?.Name ?? "?"}",
                    Setters = [$"{et.Actions.Count} action(s)"],
                    PropertyNames = [],
                };

            default:
                return null;
        }
    }

    private static string FormatCondition(System.Windows.Trigger t)
    {
        var prop = t.Property.Name;
        var val = FormatValue(t.Value);
        return string.IsNullOrEmpty(t.SourceName)
            ? $"{prop} = {val}"
            : $"{t.SourceName}.{prop} = {val}";
    }

    private static string FormatSetter(Setter s)
    {
        var target = string.IsNullOrEmpty(s.TargetName) ? "" : $"{s.TargetName}.";
        return $"{target}{s.Property?.Name ?? "?"} = {FormatValue(s.Value)}";
    }

    private static string FormatValue(object? value)
        => PropertyEnumerator.FormatValue(value, null);

    private static List<string> ExtractPropertyNames(SetterBaseCollection setters)
        => setters.OfType<Setter>()
            .Where(s => s.Property != null)
            .Select(s => s.Property!.Name)
            .ToList();

    private static bool? CheckActive(FrameworkElement element, DependencyProperty property, object? value)
    {
        try { return Equals(element.GetValue(property), value); }
        catch { return null; }
    }

    private static bool? CheckMultiActive(FrameworkElement element, MultiTrigger mt)
    {
        try
        {
            foreach (var c in mt.Conditions)
            {
                if (!Equals(element.GetValue(c.Property), c.Value))
                    return false;
            }
            return true;
        }
        catch { return null; }
    }

    private static List<TemplateTriggerInfo> ResolveVisualStateGroups(FrameworkElement element)
    {
        var results = new List<TemplateTriggerInfo>();

        FrameworkElement? templateRoot;
        try { templateRoot = VisualTreeHelper.GetChild(element, 0) as FrameworkElement; }
        catch { return results; }
        if (templateRoot == null) return results;

        var groups = VisualStateManager.GetVisualStateGroups(templateRoot);
        if (groups == null || groups.Count == 0) return results;

        foreach (VisualStateGroup group in groups)
        {
            foreach (VisualState state in group.States)
            {
                if (string.IsNullOrEmpty(state.Name)) continue;

                var setters = ExtractStoryboardActions(state.Storyboard);
                var capturedGroup = group;
                var capturedState = state;

                results.Add(new TemplateTriggerInfo
                {
                    Source = $"VSM:{group.Name}",
                    Condition = state.Name,
                    Setters = setters.Count > 0 ? setters : ["(empty state)"],
                    PropertyNames = [],
                    IsActive = ReferenceEquals(group.CurrentState, state),
                    Evaluator = () => ReferenceEquals(capturedGroup.CurrentState, capturedState),
                });
            }
        }

        return results;
    }

    private static List<string> ExtractStoryboardActions(Storyboard? sb)
    {
        if (sb == null) return [];
        var result = new List<string>();

        foreach (var timeline in sb.Children)
        {
            var targetName = Storyboard.GetTargetName(timeline);
            var targetProp = Storyboard.GetTargetProperty(timeline);
            var prefix = string.IsNullOrEmpty(targetName) ? "" : $"{targetName}.";
            var propPath = targetProp?.Path ?? "?";

            switch (timeline)
            {
                case ObjectAnimationUsingKeyFrames oakf when oakf.KeyFrames.Count > 0:
                    result.Add($"{prefix}{propPath} = {FormatValue(oakf.KeyFrames[0].Value)}");
                    break;
                case DoubleAnimation da:
                    result.Add($"{prefix}{propPath} → {da.To?.ToString("G") ?? "?"}");
                    break;
                case DoubleAnimationUsingKeyFrames dakf when dakf.KeyFrames.Count > 0:
                    result.Add($"{prefix}{propPath} → {dakf.KeyFrames[dakf.KeyFrames.Count - 1].Value:G}");
                    break;
                case ColorAnimation ca:
                    result.Add($"{prefix}{propPath} → {ca.To}");
                    break;
                case ColorAnimationUsingKeyFrames cakf when cakf.KeyFrames.Count > 0:
                    result.Add($"{prefix}{propPath} → {cakf.KeyFrames[cakf.KeyFrames.Count - 1].Value}");
                    break;
                case ThicknessAnimation ta:
                    result.Add($"{prefix}{propPath} → {FormatValue(ta.To)}");
                    break;
                default:
                    result.Add($"{prefix}{propPath} (animation)");
                    break;
            }
        }

        return result;
    }

    private static string? FindTemplateSource(FrameworkElement element, ControlTemplate template)
    {
        var current = element.Style;
        while (current != null)
        {
            foreach (var setter in current.Setters.OfType<Setter>())
            {
                if (setter.Property == Control.TemplateProperty &&
                    ReferenceEquals(setter.Value, template))
                {
                    return StyleChainResolver.FindDictionarySource(element, current);
                }
            }
            current = current.BasedOn;
        }
        return null;
    }
}
