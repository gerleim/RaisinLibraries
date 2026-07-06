namespace Raisin.StyleInspector;

public class TemplateTriggerInfo
{
    public required string Source { get; init; }
    public required string Condition { get; init; }
    public required List<string> Setters { get; init; }
    public required List<string> PropertyNames { get; init; }
    public bool? IsActive { get; set; }

    public string ActiveIndicator => IsActive switch
    {
        true => "●",
        false => "○",
        null => "◌",
    };

    public string SettersSummary => $"→ {Setters.Count} setter(s)";
    public string SettersTooltip => string.Join("\n", Setters);
}
