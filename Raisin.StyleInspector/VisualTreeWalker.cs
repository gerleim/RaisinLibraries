using System.Windows;
using System.Windows.Media;

namespace Raisin.StyleInspector;

internal static class VisualTreeWalker
{
    public static VisualTreeNode Build(DependencyObject element, int maxDepth = 50)
    {
        var node = CreateNode(element);
        if (maxDepth > 0)
        {
            try
            {
                var count = VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(element, i);
                    node.Children.Add(Build(child, maxDepth - 1));
                }
            }
            catch
            {
                // Some elements (e.g. inside HwndHost boundaries) may not allow child enumeration
            }
        }
        return node;
    }

    public static DependencyObject? GetVisualParent(DependencyObject element)
        => VisualTreeHelper.GetParent(element);

    public static DependencyObject FindVisualRoot(DependencyObject element)
    {
        var current = element;
        while (true)
        {
            var parent = VisualTreeHelper.GetParent(current);
            if (parent == null) return current;
            current = parent;
        }
    }

    public static DependencyObject FindControlRoot(DependencyObject element)
    {
        // Walk up to find a meaningful root: the nearest FrameworkElement ancestor
        // that itself is a Control/UserControl with children (the "template host").
        // Falls back to the visual tree root if no good boundary is found.
        var current = element;
        DependencyObject? bestRoot = null;

        while (current != null)
        {
            var parent = VisualTreeHelper.GetParent(current);
            if (parent == null)
                return bestRoot ?? current;

            if (current is System.Windows.Controls.Control &&
                VisualTreeHelper.GetChildrenCount(current) > 0)
            {
                bestRoot = current;
            }

            current = parent;
        }

        return bestRoot ?? element;
    }

    public static bool ExpandPathTo(VisualTreeNode root, DependencyObject target)
    {
        if (root.Element == target)
        {
            root.IsSelected = true;
            return true;
        }

        foreach (var child in root.Children)
        {
            if (ExpandPathTo(child, target))
            {
                root.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    private static VisualTreeNode CreateNode(DependencyObject element)
    {
        var typeName = element.GetType().Name;
        var name = (element as FrameworkElement)?.Name;
        var childCount = VisualTreeHelper.GetChildrenCount(element);
        var label = string.IsNullOrEmpty(name) ? typeName : $"{typeName} \"{name}\"";
        if (childCount > 0)
            label += $" ({childCount})";

        return new VisualTreeNode
        {
            Element = element,
            Label = label,
            TypeName = typeName,
            ElementName = name,
        };
    }
}
