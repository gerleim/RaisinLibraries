using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Raisin.StyleInspector;

internal sealed class ElementPicker
{
    private readonly Action<FrameworkElement> _onPicked;
    private readonly Action<FrameworkElement>? _onHovered;
    private readonly List<Window> _hookedWindows = new();
    private readonly Dictionary<UIElement, HighlightAdorner> _adorners = new();
    private FrameworkElement? _lastHovered;
    private bool _isPicking;

    public ElementPicker(Action<FrameworkElement> onPicked, Action<FrameworkElement>? onHovered = null)
    {
        _onPicked = onPicked;
        _onHovered = onHovered;
    }

    public bool IsPicking => _isPicking;

    public void Start()
    {
        if (_isPicking) return;
        _isPicking = true;

        foreach (Window window in Application.Current.Windows)
        {
            if (window is InspectorWindow) continue;
            HookWindow(window);
        }
    }

    public void Stop()
    {
        if (!_isPicking) return;
        _isPicking = false;

        foreach (var window in _hookedWindows)
        {
            window.PreviewMouseMove -= OnMouseMove;
            window.PreviewMouseLeftButtonDown -= OnMouseDown;
            window.PreviewKeyDown -= OnKeyDown;
            window.Cursor = null;
        }
        _hookedWindows.Clear();

        foreach (var (content, adorner) in _adorners)
        {
            var layer = AdornerLayer.GetAdornerLayer(content);
            layer?.Remove(adorner);
        }
        _adorners.Clear();
    }

    private void HookWindow(Window window)
    {
        window.PreviewMouseMove += OnMouseMove;
        window.PreviewMouseLeftButtonDown += OnMouseDown;
        window.PreviewKeyDown += OnKeyDown;
        window.Cursor = Cursors.Cross;
        _hookedWindows.Add(window);
    }

    private HighlightAdorner? GetOrCreateAdorner(UIElement content)
    {
        if (_adorners.TryGetValue(content, out var existing))
            return existing;

        var layer = AdornerLayer.GetAdornerLayer(content);
        if (layer == null) return null;

        var adorner = new HighlightAdorner(content);
        layer.Add(adorner);
        _adorners[content] = adorner;
        return adorner;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Window window) return;
        var root = GetVisualRoot(window);
        if (root == null) return;

        foreach (var (c, a) in _adorners)
            if (c != root) a.UpdateTarget(null);

        var point = e.GetPosition(root);
        var hit = FindElement(root, point);
        GetOrCreateAdorner(root)?.UpdateTarget(hit);

        if (hit != null && hit != _lastHovered)
        {
            _lastHovered = hit;
            _onHovered?.Invoke(hit);
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Window window) return;
        var root = GetVisualRoot(window);
        if (root == null) return;

        var point = e.GetPosition(root);
        var hit = FindElement(root, point);

        Stop();

        if (hit != null)
            _onPicked(hit);

        e.Handled = true;
    }

    private static UIElement? GetVisualRoot(Window window)
    {
        if (window.Content is UIElement content)
            return content;

        if (VisualTreeHelper.GetChildrenCount(window) > 0 &&
            VisualTreeHelper.GetChild(window, 0) is UIElement root)
            return root;

        return null;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Stop();
            e.Handled = true;
        }
    }

    private static FrameworkElement? FindElement(UIElement root, Point point)
    {
        FrameworkElement? found = null;
        VisualTreeHelper.HitTest(
            root,
            target =>
            {
                if (target is Adorner)
                    return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
                return HitTestFilterBehavior.Continue;
            },
            result =>
            {
                if (result.VisualHit is FrameworkElement fe)
                    found = fe;
                return HitTestResultBehavior.Stop;
            },
            new PointHitTestParameters(point));
        return found;
    }
}
