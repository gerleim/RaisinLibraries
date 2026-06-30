using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Raisin.WPF.Base.Controls;

public class MultiSelectFilterDropdown : Control
{
    static MultiSelectFilterDropdown()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiSelectFilterDropdown),
            new FrameworkPropertyMetadata(typeof(MultiSelectFilterDropdown)));
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty ItemContentTemplateProperty =
        DependencyProperty.Register(nameof(ItemContentTemplate), typeof(DataTemplate), typeof(MultiSelectFilterDropdown));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(MultiSelectFilterDropdown),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(nameof(SummaryText), typeof(string), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata("All"));

    public static readonly DependencyProperty ToggleBackgroundProperty =
        DependencyProperty.Register(nameof(ToggleBackground), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C))));

    public static readonly DependencyProperty ToggleBorderBrushProperty =
        DependencyProperty.Register(nameof(ToggleBorderBrush), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))));

    public static readonly DependencyProperty HoverBorderBrushProperty =
        DependencyProperty.Register(nameof(HoverBorderBrush), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77))));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC))));

    public static readonly DependencyProperty PopupBackgroundProperty =
        DependencyProperty.Register(nameof(PopupBackground), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30))));

    public static readonly DependencyProperty PopupBorderBrushProperty =
        DependencyProperty.Register(nameof(PopupBorderBrush), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))));

    public static readonly DependencyProperty ItemHoverBackgroundProperty =
        DependencyProperty.Register(nameof(ItemHoverBackground), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42))));

    public static readonly DependencyProperty ChevronBrushProperty =
        DependencyProperty.Register(nameof(ChevronBrush), typeof(Brush), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99))));

    public static readonly DependencyProperty ItemPaddingProperty =
        DependencyProperty.Register(nameof(ItemPadding), typeof(Thickness), typeof(MultiSelectFilterDropdown),
            new PropertyMetadata(new Thickness(6, 3, 6, 3)));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemContentTemplate
    {
        get => (DataTemplate?)GetValue(ItemContentTemplateProperty);
        set => SetValue(ItemContentTemplateProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public Brush ToggleBackground
    {
        get => (Brush)GetValue(ToggleBackgroundProperty);
        set => SetValue(ToggleBackgroundProperty, value);
    }

    public Brush ToggleBorderBrush
    {
        get => (Brush)GetValue(ToggleBorderBrushProperty);
        set => SetValue(ToggleBorderBrushProperty, value);
    }

    public Brush HoverBorderBrush
    {
        get => (Brush)GetValue(HoverBorderBrushProperty);
        set => SetValue(HoverBorderBrushProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public Brush PopupBackground
    {
        get => (Brush)GetValue(PopupBackgroundProperty);
        set => SetValue(PopupBackgroundProperty, value);
    }

    public Brush PopupBorderBrush
    {
        get => (Brush)GetValue(PopupBorderBrushProperty);
        set => SetValue(PopupBorderBrushProperty, value);
    }

    public Brush ItemHoverBackground
    {
        get => (Brush)GetValue(ItemHoverBackgroundProperty);
        set => SetValue(ItemHoverBackgroundProperty, value);
    }

    public Brush ChevronBrush
    {
        get => (Brush)GetValue(ChevronBrushProperty);
        set => SetValue(ChevronBrushProperty, value);
    }

    public Thickness ItemPadding
    {
        get => (Thickness)GetValue(ItemPaddingProperty);
        set => SetValue(ItemPaddingProperty, value);
    }

    public event EventHandler? SelectionChanged;

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (MultiSelectFilterDropdown)d;
        if ((bool)e.NewValue)
            Mouse.Capture(self, CaptureMode.SubTree);
        else
            Mouse.Capture(null);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (!IsDropDownOpen) return;

        // Check if click is inside the popup
        if (_popup != null && _popup.Child is UIElement popupChild)
        {
            var pos = e.GetPosition(popupChild);
            if (pos.X >= 0 && pos.Y >= 0 &&
                pos.X <= popupChild.RenderSize.Width &&
                pos.Y <= popupChild.RenderSize.Height)
                return;
        }

        // Check if click is inside the toggle button area (let it handle toggle)
        var togglePos = e.GetPosition(this);
        if (togglePos.X >= 0 && togglePos.Y >= 0 &&
            togglePos.X <= ActualWidth && togglePos.Y <= ActualHeight)
            return;

        // Click is outside — close
        IsDropDownOpen = false;
        e.Handled = true;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (MultiSelectFilterDropdown)d;
        if (e.OldValue is IEnumerable oldItems)
        {
            foreach (var obj in oldItems)
                if (obj is SelectableFilterItem item)
                    item.PropertyChanged -= self.OnItemPropertyChanged;
        }
        if (e.NewValue is IEnumerable newItems)
        {
            foreach (var obj in newItems)
                if (obj is SelectableFilterItem item)
                    item.PropertyChanged += self.OnItemPropertyChanged;
        }
        self.UpdateSummary();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableFilterItem.IsSelected))
            UpdateSummary();
    }

    private ItemsControl? _itemsList;
    private System.Windows.Controls.Primitives.Popup? _popup;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _itemsList = GetTemplateChild("PART_ItemsList") as ItemsControl;
        _popup = GetTemplateChild("PART_Popup") as System.Windows.Controls.Primitives.Popup;

        if (_itemsList != null)
            _itemsList.PreviewMouseLeftButtonDown += OnItemsListClick;
    }

    private void OnItemsListClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        // Find which SelectableFilterItem was clicked
        SelectableFilterItem? item = null;
        var walk = source;
        while (walk != null && walk != _itemsList)
        {
            if (walk is FrameworkElement fe && fe.DataContext is SelectableFilterItem sfi)
            {
                item = sfi;
                break;
            }
            walk = VisualTreeHelper.GetParent(walk);
        }
        if (item == null) return;

        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        // If Shift is held, always handle ourselves (even on checkbox)
        // If no Shift and clicking a CheckBox, let it handle its own toggle
        if (!shift)
        {
            walk = source;
            while (walk != null && walk != _itemsList)
            {
                if (walk is CheckBox) return;
                walk = VisualTreeHelper.GetParent(walk);
            }
        }

        HandleItemClick(item, shift);
        e.Handled = true;
    }

    private void HandleItemClick(SelectableFilterItem item, bool shiftHeld)
    {
        if (shiftHeld)
            SelectThisAndAbove(item);
        else
            item.IsSelected = !item.IsSelected;

        UpdateSummary();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SelectThisAndAbove(SelectableFilterItem target)
    {
        if (ItemsSource == null) return;

        var items = new List<SelectableFilterItem>();
        foreach (var obj in ItemsSource)
            if (obj is SelectableFilterItem item)
                items.Add(item);

        // Check if target and all items above it are already selected
        bool allAboveSelected = true;
        foreach (var item in items)
        {
            if (!item.IsSelected) { allAboveSelected = false; break; }
            if (item == target) break;
        }

        foreach (var item in items)
            item.SuppressChangeNotification = true;

        if (allAboveSelected)
        {
            foreach (var item in items)
                item.IsSelected = false;
        }
        else
        {
            bool found = false;
            foreach (var item in items)
            {
                item.IsSelected = !found;
                if (item == target) found = true;
            }
        }

        foreach (var item in items)
            item.SuppressChangeNotification = false;
    }

    internal void UpdateSummary()
    {
        if (ItemsSource == null) { SummaryText = "All"; return; }

        var selected = new List<SelectableFilterItem>();
        foreach (var obj in ItemsSource)
        {
            if (obj is SelectableFilterItem { IsSelected: true } item)
                selected.Add(item);
        }

        SummaryText = selected.Count switch
        {
            0 => "All",
            1 => selected[0].Label,
            _ => $"{selected.Count} selected"
        };
    }

    public void ClearSelection()
    {
        if (ItemsSource == null) return;
        foreach (var obj in ItemsSource)
        {
            if (obj is SelectableFilterItem item)
                item.IsSelected = false;
        }
        UpdateSummary();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HasSelection()
    {
        if (ItemsSource == null) return false;
        foreach (var obj in ItemsSource)
        {
            if (obj is SelectableFilterItem { IsSelected: true })
                return true;
        }
        return false;
    }
}
