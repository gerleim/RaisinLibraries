using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Raisin.StyleInspector;

internal sealed class HighlightAdorner : Adorner
{
    private static readonly Pen HighlightPen;
    private static readonly Brush FillBrush;

    private Rect _bounds;

    static HighlightAdorner()
    {
        HighlightPen = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)), 2);
        HighlightPen.Freeze();
        FillBrush = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x7A, 0xCC));
        FillBrush.Freeze();
    }

    public HighlightAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public void UpdateTarget(FrameworkElement? target)
    {
        if (target == null)
        {
            _bounds = Rect.Empty;
        }
        else
        {
            try
            {
                var transform = target.TransformToAncestor(AdornedElement);
                _bounds = transform.TransformBounds(new Rect(target.RenderSize));
            }
            catch
            {
                _bounds = Rect.Empty;
            }
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_bounds.IsEmpty) return;
        dc.DrawRectangle(FillBrush, HighlightPen, _bounds);
    }
}
