namespace Raisin.WPF.Base.Models;

/// <summary>
/// Captures the pane split structure inside a floating window so it can be
/// rebuilt when undoing a close. Leaf nodes hold ContentIds (documents in a
/// single pane); branch nodes hold an Orientation and child nodes.
/// </summary>
public class FloatingPaneNode
{
    public bool IsLeaf { get; set; }
    /// <summary>0 = Horizontal, 1 = Vertical (matches System.Windows.Controls.Orientation).</summary>
    public int Orientation { get; set; }
    public double DockWidth { get; set; } = 1.0;
    public double DockHeight { get; set; } = 1.0;
    public bool DockWidthIsStar { get; set; } = true;
    public bool DockHeightIsStar { get; set; } = true;
    public List<FloatingPaneNode>? Children { get; set; }
    public List<string>? ContentIds { get; set; }
}
