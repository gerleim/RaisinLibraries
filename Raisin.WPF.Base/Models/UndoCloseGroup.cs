namespace Raisin.WPF.Base.Models;

/// <summary>
/// Groups the records closed in a single dispatcher frame (e.g. closing a floating window)
/// together with the optional floating-window layout tree.
/// </summary>
public class UndoCloseGroup<T> where T : class, IUndoRecord
{
    public List<T> Records { get; } = [];
    public FloatingPaneNode? FloatingLayout { get; set; }
    public double FloatingLeft { get; set; }
    public double FloatingTop { get; set; }
    public double FloatingWidth { get; set; }
    public double FloatingHeight { get; set; }
    public string FloatingTitle { get; set; } = "";
}
