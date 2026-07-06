using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Raisin.StyleInspector;

internal static class VisualTreeWalker
{
    public static VisualTreeNode? BuildTemplateTree(FrameworkElement element)
    {
        if (element is not Control control || control.Template == null)
            return null;

        var root = CreateNode(control);
        AddTemplateChildren(root, control, control);
        return root.Children.Count > 0 ? root : null;
    }

    private static void AddTemplateChildren(VisualTreeNode parent, DependencyObject current, Control owner)
    {
        try
        {
            var count = VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is FrameworkElement fe && ReferenceEquals(fe.TemplatedParent, owner))
                {
                    var node = CreateNode(child);
                    parent.Children.Add(node);
                    AddTemplateChildren(node, child, owner);
                }
            }
        }
        catch { }
    }

    public static VisualTreeNode Build(DependencyObject element, int maxDepth = 100)
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

    public static DependencyObject FindAncestor(DependencyObject element, int levels)
    {
        var current = element;
        for (int i = 0; i < levels; i++)
        {
            var parent = VisualTreeHelper.GetParent(current);
            if (parent == null) break;
            current = parent;
        }
        return current;
    }

    public static bool ExpandPathTo(VisualTreeNode root, DependencyObject target)
    {
        var path = new List<VisualTreeNode>();
        if (!FindPath(root, target, path))
            return false;

        foreach (var node in path)
            node.IsExpanded = true;

        path[^1].IsSelected = true;
        return true;
    }

    private static bool FindPath(VisualTreeNode node, DependencyObject target, List<VisualTreeNode> path)
    {
        path.Add(node);
        if (node.Element == target)
            return true;

        foreach (var child in node.Children)
        {
            if (FindPath(child, target, path))
                return true;
        }

        path.RemoveAt(path.Count - 1);
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
