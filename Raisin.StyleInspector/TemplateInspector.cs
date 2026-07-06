using System.Windows;
using System.Windows.Controls;

namespace Raisin.StyleInspector;

public class TemplateInfo
{
    public required string TargetType { get; init; }
    public string? DictionarySource { get; init; }
    public List<TemplateTriggerInfo> Triggers { get; init; } = new();
}

internal static class TemplateInspector
{
    public static TemplateInfo? Resolve(FrameworkElement element)
    {
        var template = (element as Control)?.Template;
        if (template == null) return null;

        var info = new TemplateInfo
        {
            TargetType = template.TargetType?.Name ?? "?",
            DictionarySource = FindTemplateSource(element, template),
        };

        foreach (var trigger in template.Triggers)
        {
            var triggerInfo = ResolveTrigger(trigger, element);
            if (triggerInfo != null)
                info.Triggers.Add(triggerInfo);
        }

        return info;
    }

    private static TemplateTriggerInfo? ResolveTrigger(TriggerBase trigger, FrameworkElement element)
    {
        switch (trigger)
        {
            case System.Windows.Trigger t:
                return new TemplateTriggerInfo
                {
                    Condition = FormatCondition(t),
                    Setters = t.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(t.Setters),
                    IsActive = string.IsNullOrEmpty(t.SourceName)
                        ? CheckActive(element, t.Property, t.Value) : null,
                };

            case MultiTrigger mt:
            {
                var conditions = mt.Conditions.Select(c =>
                    string.IsNullOrEmpty(c.SourceName)
                        ? $"{c.Property.Name} = {FormatValue(c.Value)}"
                        : $"{c.SourceName}.{c.Property.Name} = {FormatValue(c.Value)}");
                var allOnSelf = mt.Conditions.All(c => string.IsNullOrEmpty(c.SourceName));
                return new TemplateTriggerInfo
                {
                    Condition = string.Join(" AND ", conditions),
                    Setters = mt.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(mt.Setters),
                    IsActive = allOnSelf ? CheckMultiActive(element, mt) : null,
                };
            }

            case DataTrigger dt:
                return new TemplateTriggerInfo
                {
                    Condition = $"[Binding] = {FormatValue(dt.Value)}",
                    Setters = dt.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(dt.Setters),
                };

            case MultiDataTrigger mdt:
            {
                var conditions = mdt.Conditions.Select(c => $"[Binding] = {FormatValue(c.Value)}");
                return new TemplateTriggerInfo
                {
                    Condition = string.Join(" AND ", conditions),
                    Setters = mdt.Setters.OfType<Setter>().Select(FormatSetter).ToList(),
                    PropertyNames = ExtractPropertyNames(mdt.Setters),
                };
            }

            case EventTrigger et:
                return new TemplateTriggerInfo
                {
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
