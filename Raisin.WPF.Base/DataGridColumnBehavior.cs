using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Raisin.WPF.Base;

/// <summary>
/// Attached behavior that persists DataGrid column widths, per window instance.
/// Set GridId to a string identifier to enable the behavior.
///
/// A column is remembered either as an exact pixel width — what dragging the gripper leaves — or as
/// a sizing MODE: fit the header, fit the content, or fit both. The distinction matters because a
/// pixel width is a measurement taken once, on some earlier day, against whatever rows happened to
/// be realized; a mode is a standing instruction re-applied on every load, so a column set to fit
/// its content still fits it a week later. Right-click a header to choose.
///
/// Settings are keyed <c>{ContentId}.{GridId}</c> — the ContentId found by walking up to the owning
/// ToolWindowViewModel — so two windows showing the same grid size independently, and
/// GridSettingsService.Prune drops the entries of windows that no longer exist. Within a grid,
/// columns are keyed by identity (sort path, else header text, else position) rather than by index,
/// so reordering no longer hands each column its neighbour's width.
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

        var key = BuildKey(dataGrid, gridId);
        if (key is not null)
            ApplyState(dataGrid, GridSettingsService.Load(key));

        AddHeaderContextMenu(dataGrid, gridId);

        // Persist a width the user dragged. The gripper between two headers is a Thumb, and its
        // DragCompleted bubbles to the grid — the only signal WPF offers that a resize finished.
        // Deferred past the layout pass because a double-click on the gripper is WPF setting the
        // column to Auto and it raises DragCompleted too: saving inline would store the width from
        // before that measure rather than the fitted result.
        dataGrid.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((_, args) =>
            {
                var column = FindColumn(args.OriginalSource as DependencyObject);
                if (column is null) return;
                dataGrid.Dispatcher.InvokeAsync(
                    () => SaveColumn(dataGrid, gridId, column, ColumnSizeMode.Fixed),
                    DispatcherPriority.ContextIdle);
            }),
            handledEventsToo: true);
    }

    // --- state ------------------------------------------------------------------------------

    private static void ApplyState(DataGrid dataGrid, GridState? state)
    {
        if (state is null)
            return;

        if (state.Columns.Count > 0)
        {
            foreach (var col in dataGrid.Columns)
            {
                if (state.Columns.TryGetValue(ColumnKey(dataGrid, col), out var setting))
                    col.Width = ToLength(setting);
            }
            return;
        }

        // Saved before per-column settings existed: widths by index.
        foreach (var (index, width) in state.ColumnWidths)
        {
            if (index >= 0 && index < dataGrid.Columns.Count && width > 0)
                dataGrid.Columns[index].Width = new DataGridLength(width);
        }
    }

    private static DataGridLength ToLength(ColumnSetting setting) => setting.Mode switch
    {
        ColumnSizeMode.Header => DataGridLength.SizeToHeader,
        ColumnSizeMode.Content => DataGridLength.SizeToCells,
        ColumnSizeMode.Both => DataGridLength.Auto,
        _ => setting.Width > 0 ? new DataGridLength(setting.Width) : DataGridLength.Auto,
    };

    /// <summary>
    /// Records one column's setting, leaving every other column's alone — a drag must not overwrite
    /// the modes its neighbours were given.
    /// </summary>
    private static void SaveColumn(DataGrid dataGrid, string gridId, DataGridColumn column, ColumnSizeMode mode)
    {
        var key = BuildKey(dataGrid, gridId);
        if (key is null)
            return;

        var state = GridSettingsService.Load(key) ?? new GridState();

        // First write after an upgrade: fold the index-keyed widths into identity-keyed ones so the
        // rest of the grid is not lost the moment one column is touched.
        if (state.Columns.Count == 0 && state.ColumnWidths.Count > 0)
        {
            for (int i = 0; i < dataGrid.Columns.Count; i++)
            {
                if (state.ColumnWidths.TryGetValue(i, out var w) && w > 0)
                    state.Columns[ColumnKey(dataGrid, dataGrid.Columns[i])] =
                        new ColumnSetting { Mode = ColumnSizeMode.Fixed, Width = w };
            }
        }

        state.Columns[ColumnKey(dataGrid, column)] = new ColumnSetting
        {
            Mode = mode,
            Width = mode == ColumnSizeMode.Fixed ? column.ActualWidth : 0,
        };

        GridSettingsService.Save(key, state);
    }

    private static void SetColumnMode(DataGrid dataGrid, string gridId, DataGridColumn column, ColumnSizeMode mode)
    {
        column.Width = ToLength(new ColumnSetting { Mode = mode });
        SaveColumn(dataGrid, gridId, column, mode);
    }

    // --- context menu -----------------------------------------------------------------------

    private static void AddHeaderContextMenu(DataGrid dataGrid, string gridId)
    {
        var style = new Style(typeof(DataGridColumnHeader));

        var existingStyle = dataGrid.ColumnHeaderStyle
            ?? (Style)dataGrid.FindResource(typeof(DataGridColumnHeader));
        if (existingStyle is not null)
            style.BasedOn = existingStyle;

        var menu = new ContextMenu();

        // Names the column and says how it is currently sized, so the menu answers "what is this
        // set to?" before it offers to change it.
        var current = new MenuItem { IsEnabled = false };
        menu.Items.Add(current);
        menu.Items.Add(new Separator());

        var toHeader = AddModeItem(menu, "Fit to header", dataGrid, gridId, ColumnSizeMode.Header);
        var toContent = AddModeItem(menu, "Fit to content", dataGrid, gridId, ColumnSizeMode.Content);
        var toBoth = AddModeItem(menu, "Fit to both", dataGrid, gridId, ColumnSizeMode.Both);

        AddItem(menu, "Reset this column", m => WithColumn(m, c => ResetColumn(dataGrid, gridId, c)));

        menu.Items.Add(new Separator());

        AddItem(menu, "Auto-fit all columns", _ => AutoFitColumns(dataGrid, gridId));
        AddItem(menu, "Reset all columns", _ => ResetColumns(dataGrid, gridId));

        // One menu serves every header, so what it shows can only be decided as it opens, from the
        // column it was opened over. The ticks read the column's live UnitType rather than the
        // saved setting: that is the truth even for a column nobody has configured yet.
        menu.Opened += (_, _) =>
        {
            var column = (menu.PlacementTarget as DataGridColumnHeader)?.Column;
            current.Header = column is null
                ? "No column"
                : $"{ColumnName(column)} — {DescribeWidth(column)}";

            var unit = column?.Width.UnitType;
            toHeader.IsChecked = unit == DataGridLengthUnitType.SizeToHeader;
            toContent.IsChecked = unit == DataGridLengthUnitType.SizeToCells;
            toBoth.IsChecked = unit == DataGridLengthUnitType.Auto;
        };

        style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));
        dataGrid.ColumnHeaderStyle = style;
    }

    private static MenuItem AddModeItem(ContextMenu menu, string text, DataGrid dataGrid,
        string gridId, ColumnSizeMode mode)
    {
        var item = new MenuItem { Header = text, IsCheckable = true };
        item.Click += (s, _) =>
        {
            var clicked = (MenuItem)s;
            WithColumn(clicked, c => SetColumnMode(dataGrid, gridId, c, mode));
            // A checkable item toggles itself on click; the mode was just applied either way, so
            // the tick must show set rather than whatever the toggle landed on.
            clicked.IsChecked = true;
        };
        menu.Items.Add(item);
        return item;
    }

    /// <summary>What the column is doing right now, in the menu's words.</summary>
    private static string DescribeWidth(DataGridColumn column) => column.Width.UnitType switch
    {
        DataGridLengthUnitType.SizeToHeader => "fits header",
        DataGridLengthUnitType.SizeToCells => "fits content",
        DataGridLengthUnitType.Auto => "fits both",
        DataGridLengthUnitType.Star => "proportional",
        _ => $"{column.ActualWidth:F0} px",
    };

    private static string ColumnName(DataGridColumn column) =>
        column.Header is string h && !string.IsNullOrWhiteSpace(h)
            ? h
            : !string.IsNullOrWhiteSpace(column.SortMemberPath) ? column.SortMemberPath : "Column";

    private static void AddItem(ContextMenu menu, string header, Action<MenuItem> onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (s, _) => onClick((MenuItem)s);
        menu.Items.Add(item);
    }

    private static void WithColumn(MenuItem item, Action<DataGridColumn> action)
    {
        if (ItemsControl.ItemsControlFromItemContainer(item) is ContextMenu menu
            && menu.PlacementTarget is DataGridColumnHeader header
            && header.Column is { } column)
        {
            action(column);
        }
    }

    private static void ResetColumn(DataGrid dataGrid, string gridId, DataGridColumn column)
    {
        var index = dataGrid.Columns.IndexOf(column);
        if (DefaultWidths.TryGetValue(gridId, out var defaults) && index >= 0 && index < defaults.Count)
            column.Width = defaults[index];

        var key = BuildKey(dataGrid, gridId);
        if (key is null)
            return;

        var state = GridSettingsService.Load(key);
        if (state is null)
            return;

        if (state.Columns.Remove(ColumnKey(dataGrid, column)))
            GridSettingsService.Save(key, state);
    }

    private static void AutoFitColumns(DataGrid dataGrid, string gridId)
    {
        foreach (var col in dataGrid.Columns)
            col.Width = DataGridLength.Auto;

        // Wait for the layout pass, then freeze to fixed pixel widths. Freezing rather than leaving
        // them Auto is deliberate: this item means "fit once, now", and the per-column modes above
        // are how you ask for fitting that keeps up with the data.
        dataGrid.Dispatcher.InvokeAsync(() =>
        {
            var key = BuildKey(dataGrid, gridId);
            if (key is null) return;

            var state = GridSettingsService.Load(key) ?? new GridState();
            state.Columns.Clear();

            foreach (var col in dataGrid.Columns)
            {
                var actual = col.ActualWidth + 1;
                col.Width = new DataGridLength(actual);
                state.Columns[ColumnKey(dataGrid, col)] =
                    new ColumnSetting { Mode = ColumnSizeMode.Fixed, Width = actual };
            }

            GridSettingsService.Save(key, state);
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

    // --- identity ---------------------------------------------------------------------------

    /// <summary>
    /// A column's stable name. The sort path first because it survives a header being reworded, then
    /// the header for template columns that have none, and position only as a last resort — the
    /// sparkline column has neither.
    /// </summary>
    private static string ColumnKey(DataGrid dataGrid, DataGridColumn column)
    {
        if (!string.IsNullOrWhiteSpace(column.SortMemberPath))
            return column.SortMemberPath;
        if (column.Header is string header && !string.IsNullOrWhiteSpace(header))
            return header;
        return $"#{dataGrid.Columns.IndexOf(column)}";
    }

    private static DataGridColumn? FindColumn(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is DataGridColumnHeader header)
                return header.Column;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
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
