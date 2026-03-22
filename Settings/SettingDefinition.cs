namespace Raisin.WPF.Base.Settings;

public record SettingDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string PropertyName { get; init; }
    public required SettingEditorType EditorType { get; init; }
    public string[]? Choices { get; init; }
    public int Order { get; init; }
    public int? MinInt { get; init; }
    public int? MaxInt { get; init; }
    public double? MinDouble { get; init; }
    public double? MaxDouble { get; init; }
    public string? FormatString { get; init; }
}
