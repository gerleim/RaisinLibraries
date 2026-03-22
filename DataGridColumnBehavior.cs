using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Raisin.WPF.Base;

/// <summary>
/// Attached behavior that enables auto-fit and persist column widths on DataGrids.
/// Set GridId to a string identifier to enable the behavior.
/// Adds a context menu to column headers with "Auto-fit Columns" and "Reset Column Widths".
/// </summary>
public static class DataGridColumnBehavior
{
    // Stores default widths from XAML, keyed by GridId
    private static readonly Dictionary<string, List<DataGridLength>> DefaultWidths = [];

    public static readonly DependencyProperty GridIdProperty =
        DependencyProperty.RegisterAttached(
            "GridId",
            typeof(string),
            typeof(DataGridColumnBehavior),
            new PropertyMetadata(null, OnGridIdChanged));

    public static string? GetGridId(DependencyObject obj) => (string?)obj.GetValue(GridIdProperty);
    public static void SetGridId(DependencyObject obj, string? value) => obj.SetValue(GridIdProperty, value);

    private static void OnGridIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid || e.NewValue is not string gridId || string.IsNullOrEmpty(gridId))
            return;

        if (!dataGrid.IsLoaded)
        {
            void handler(object sender, RoutedEventArgs args)
            {
                dataGrid.Loaded -= handler;
                Initialize(dataGrid, gridId);
            }
            dataGrid.Loaded += handler;
        }
        else
        {
            Initialize(dataGrid, gridId);
        }
    }

    private static void Initialize(DataGrid dataGrid, string gridId)
    {
        // Snapshot default widths
        var defaults = new List<DataGridLength>(dataGrid.Columns.Count);
        foreach (var col in dataGrid.Columns)
            defaults.Add(col.Width);
        DefaultWidths[gridId] = defaults;

        // Build persistence key
        var key = BuildKey(dataGrid, gridId);

        // Apply persisted widths if available
        if (key is not null)
        {
            var state = GridSettingsService.Load(key);
            if (state is not null)
                ApplyWidths(dataGrid, state.ColumnWidths);
        }

        // Add context menu to column headers
        AddHeaderContextMenu(dataGrid, gridId);
    }

    private static void AddHeaderContextMenu(DataGrid dataGrid, string gridId)
    {
        var style = new Style(typeof(DataGridColumnHeader));

        // Base on existing style if present
        var existingStyle = dataGrid.ColumnHeaderStyle
            ?? (Style)dataGrid.FindResource(typeof(DataGridColumnHeader));
        if (existingStyle is not null)
            style.BasedOn = existingStyle;

        var menu = new ContextMenu();

        var autoFit = new MenuItem { Header = "Auto-fit Columns" };
        autoFit.Click += (_, _) => AutoFitColumns(dataGrid, gridId);
        menu.Items.Add(autoFit);

        var reset = new MenuItem { Header = "Reset Column Widths" };
        reset.Click += (_, _) => ResetColumns(dataGrid, gridId);
        menu.Items.Add(reset);

        style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));
        dataGrid.ColumnHeaderStyle = style;
    }

    private static void AutoFitColumns(DataGrid dataGrid, string gridId)
    {
        // Set all columns to Auto to trigger measurement
        foreach (var col in dataGrid.Columns)
            col.Width = DataGridLength.Auto;

        // Wait for layout pass, then freeze to fixed pixel widths
        dataGrid.Dispatcher.InvokeAsync(() =>
        {
            var widths = new Dictionary<int, double>();
            for (int i = 0; i < dataGrid.Columns.Count; i++)
            {
                var actual = dataGrid.Columns[i].ActualWidth + 1;
                dataGrid.Columns[i].Width = new DataGridLength(actual);
                widths[i] = actual;
            }

            var key = BuildKey(dataGrid, gridId);
            if (key is not null)
                GridSettingsService.Save(key, widths);
        }, DispatcherPriority.ContextIdle);
    }

    private static void ResetColumns(DataGrid dataGrid, string gridId)
    {
        if (!DefaultWidths.TryGetValue(gridId, out var defaults))
            return;

        for (int i = 0; i < dataGrid.Columns.Count && i < defaults.Count; i++)
            dataGrid.Columns[i].Width = defaults[i];

        var key = BuildKey(dataGrid, gridId);
        if (key is not null)
            GridSettingsService.Remove(key);
    }

    private static void ApplyWidths(DataGrid dataGrid, Dictionary<int, double> widths)
    {
        foreach (var (index, width) in widths)
        {
            if (index >= 0 && index < dataGrid.Columns.Count)
                dataGrid.Columns[index].Width = new DataGridLength(width);
        }
    }

    private static string? BuildKey(DataGrid dataGrid, string gridId)
    {
        var contentId = FindContentId(dataGrid);
        if (contentId is null)
            return gridId; // fallback for anchorables without ContentId
        return $"{contentId}.{gridId}";
    }

    private static string? FindContentId(DependencyObject obj)
    {
        // Walk up visual tree to find a ToolWindowViewModel DataContext
        DependencyObject? current = obj;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is ToolWindowViewModel vm)
                return vm.ContentId;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
