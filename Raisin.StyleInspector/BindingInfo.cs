using System.Windows.Media;

namespace Raisin.StyleInspector;

public class BindingInfo
{
    public required string PropertyName { get; init; }
    public required string Path { get; init; }
    public required string SourceDescription { get; init; }
    public string? Mode { get; init; }
    public string? Converter { get; init; }
    public string? CurrentValue { get; init; }
    public required string StatusIndicator { get; init; }
    public required Brush StatusBrush { get; init; }
    public bool HasError { get; init; }
    public string? Tooltip { get; init; }
}
