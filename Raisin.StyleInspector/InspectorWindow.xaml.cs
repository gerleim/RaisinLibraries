using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Raisin.StyleInspector;

public partial class InspectorWindow : Window
{
    private readonly ObservableCollection<InspectedProperty> _properties = new();
    private ICollectionView? _view;
    private ElementPicker? _picker;

    public InspectorWindow()
    {
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_properties);
        _view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        _view.Filter = FilterProperty;
        PropertyList.ItemsSource = _view;
    }

    private void OnPickClick(object sender, RoutedEventArgs e)
    {
        if (_picker?.IsPicking == true)
            StopPicking();
        else
            StartPicking();
    }

    private void StartPicking()
    {
        _picker = new ElementPicker(OnElementPicked, OnElementHovered);
        _picker.Start();
        PickButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
        StatusText.Text = "Click an element to inspect…";
    }

    private void StopPicking()
    {
        _picker?.Stop();
        _picker = null;
        PickButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
    }

    private void ShowElement(FrameworkElement element)
    {
        var name = string.IsNullOrEmpty(element.Name) ? "" : $" \"{element.Name}\"";
        ElementType.Text = $"{element.GetType().Name}{name}";
        ElementDetail.Text = element.GetType().FullName;
        ElementInfoPanel.Visibility = Visibility.Visible;

        _properties.Clear();
        foreach (var prop in PropertyEnumerator.Enumerate(element))
            _properties.Add(prop);

        UpdateStatus();
        UpdateResetAllVisibility();
    }

    private void OnElementHovered(FrameworkElement element) => ShowElement(element);

    private void OnElementPicked(FrameworkElement element)
    {
        StopPicking();
        ShowElement(element);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshFilter();
    private void OnFilterChanged(object sender, RoutedEventArgs e) => RefreshFilter();

    private void RefreshFilter()
    {
        _view?.Refresh();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var total = _properties.Count;
        var visible = _view?.Cast<object>().Count() ?? total;
        StatusText.Text = visible == total
            ? $"{total} properties"
            : $"{visible} of {total} properties";
    }

    private bool FilterProperty(object obj)
    {
        if (obj is not InspectedProperty prop) return false;
        if (HideDefaultsBox.IsChecked == true && prop.IsDefault) return false;

        var search = SearchBox.Text;
        if (!string.IsNullOrEmpty(search))
            return prop.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private void OnValueKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb && tb.DataContext is InspectedProperty prop)
        {
            if (prop.ApplyText(tb.Text))
            {
                UpdateResetAllVisibility();
                Keyboard.ClearFocus();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && sender is TextBox tbEsc && tbEsc.DataContext is InspectedProperty propEsc)
        {
            tbEsc.Text = propEsc.DisplayValue;
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void OnValueLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is InspectedProperty prop)
        {
            if (tb.Text != prop.DisplayValue)
                prop.ApplyText(tb.Text);
            UpdateResetAllVisibility();
        }
    }

    private void OnBoolChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is InspectedProperty prop)
        {
            prop.ApplyValue(cb.IsChecked == true);
            UpdateResetAllVisibility();
        }
    }

    private void OnEnumChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.DataContext is InspectedProperty prop
            && combo.SelectedItem != null && !Equals(combo.SelectedItem, prop.Value))
        {
            prop.ApplyValue(combo.SelectedItem);
            UpdateResetAllVisibility();
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is InspectedProperty prop)
        {
            prop.ResetValue();
            UpdateResetAllVisibility();
        }
    }

    private void OnResetAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var prop in _properties.Where(p => p.IsEdited).ToList())
            prop.ResetValue();
        UpdateResetAllVisibility();
    }

    private void UpdateResetAllVisibility()
    {
        ResetAllButton.Visibility = _properties.Any(p => p.IsEdited)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopPicking();
        base.OnClosed(e);
    }
}
