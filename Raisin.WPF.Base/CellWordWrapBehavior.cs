using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Raisin.WPF.Base;

public static class CellWordWrapBehavior
{
    public static readonly DependencyProperty EnableWordWrapProperty =
        DependencyProperty.RegisterAttached(
            "EnableWordWrap",
            typeof(bool),
            typeof(CellWordWrapBehavior),
            new PropertyMetadata(false, OnEnableWordWrapChanged));

    public static bool GetEnableWordWrap(DependencyObject obj) => (bool)obj.GetValue(EnableWordWrapProperty);
    public static void SetEnableWordWrap(DependencyObject obj, bool value) => obj.SetValue(EnableWordWrapProperty, value);

    private static void OnEnableWordWrapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;

        if (e.NewValue is true)
        {
            textBlock.TextWrapping = TextWrapping.Wrap;

            if (textBlock.IsLoaded)
                AttachToCell(textBlock);
            else
                textBlock.Loaded += OnTextBlockLoaded;
        }
        else
        {
            textBlock.Loaded -= OnTextBlockLoaded;
            DetachFromCell(textBlock);
            textBlock.ClearValue(FrameworkElement.MaxWidthProperty);
            textBlock.TextWrapping = TextWrapping.NoWrap;
        }
    }

    private static void OnTextBlockLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock tb) return;
        tb.Loaded -= OnTextBlockLoaded;
        AttachToCell(tb);
    }

    private static void AttachToCell(TextBlock textBlock)
    {
        var cell = FindAncestor<DataGridCell>(textBlock);
        if (cell is null) return;

        var grid = FindAncestor<DataGrid>(cell);
        if (grid is not null && !double.IsNaN(grid.RowHeight))
            grid.RowHeight = double.NaN;

        cell.SizeChanged -= OnCellSizeChanged;
        cell.SizeChanged += OnCellSizeChanged;

        textBlock.Unloaded += OnTextBlockUnloaded;

        UpdateMaxWidth(textBlock, cell);
    }

    private static void DetachFromCell(TextBlock textBlock)
    {
        var cell = FindAncestor<DataGridCell>(textBlock);
        if (cell is null) return;
        cell.SizeChanged -= OnCellSizeChanged;
        textBlock.Unloaded -= OnTextBlockUnloaded;
    }

    private static void OnTextBlockUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock tb) return;
        tb.Unloaded -= OnTextBlockUnloaded;

        var cell = FindAncestor<DataGridCell>(tb);
        if (cell is not null)
            cell.SizeChanged -= OnCellSizeChanged;

        tb.Loaded += OnTextBlockLoaded;
    }

    private static void OnCellSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not DataGridCell cell) return;
        if (!e.WidthChanged) return;

        var textBlock = FindDescendant<TextBlock>(cell);
        if (textBlock is null || !GetEnableWordWrap(textBlock)) return;

        UpdateMaxWidth(textBlock, cell);
    }

    private static void UpdateMaxWidth(TextBlock textBlock, DataGridCell cell)
    {
        var width = cell.ActualWidth;
        if (width <= 0) return;

        var maxWidth = width - 4;
        if (maxWidth > 0)
            textBlock.MaxWidth = maxWidth;
    }

    private static T? FindAncestor<T>(DependencyObject obj) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(obj);
        while (parent is not null)
        {
            if (parent is T target)
                return target;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is T target)
                return target;
            var result = FindDescendant<T>(child);
            if (result is not null)
                return result;
        }
        return null;
    }
}
