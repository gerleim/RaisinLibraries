using System.Windows.Media;

namespace Raisin.StyleInspector;

public enum DiffKind
{
    Match,
    ValueDiffers,
    SourceDiffers,
    OnlyA,
    OnlyB,
}

public class ComparedProperty
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required DiffKind Diff { get; init; }

    public string? DisplayValueA { get; init; }
    public string? SourceTagA { get; init; }
    public string? SourceTooltipA { get; init; }
    public Brush? SourceBrushA { get; init; }
    public Brush? ColorPreviewA { get; init; }

    public string? DisplayValueB { get; init; }
    public string? SourceTagB { get; init; }
    public string? SourceTooltipB { get; init; }
    public Brush? SourceBrushB { get; init; }
    public Brush? ColorPreviewB { get; init; }

    public bool IsMatch => Diff == DiffKind.Match;
    public bool IsDifferent => Diff != DiffKind.Match;
}
