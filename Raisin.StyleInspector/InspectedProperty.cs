using System.Windows;
using System.Windows.Media;

namespace Raisin.StyleInspector;

public class InspectedProperty
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public object? Value { get; init; }
    public required string DisplayValue { get; init; }
    public required BaseValueSource Source { get; init; }
    public required string SourceTag { get; init; }
    public required Brush SourceBrush { get; init; }
    public Brush? ColorPreview { get; init; }
    public string? ResourceKey { get; init; }
    public required DependencyProperty Property { get; init; }
    public bool IsDefault => Source == BaseValueSource.Default;
}
