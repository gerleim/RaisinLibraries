using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Raisin.WPF.Base.Settings;

public partial class SettingsListControl : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable),
            typeof(SettingsListControl), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty CategoriesProperty =
        DependencyProperty.Register(nameof(Categories), typeof(IEnumerable),
            typeof(SettingsListControl), new PropertyMetadata(null, OnCategoriesChanged));

    public static readonly DependencyProperty SelectedCategoryProperty =
        DependencyProperty.Register(nameof(SelectedCategory), typeof(string),
            typeof(SettingsListControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedCategoryChanged));

    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.Register(nameof(ItemTemplateSelector), typeof(DataTemplateSelector),
            typeof(SettingsListControl), new PropertyMetadata(null, OnTemplateSelectorChanged));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string),
            typeof(SettingsListControl),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IEnumerable? Categories
    {
        get => (IEnumerable?)GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    public string? SelectedCategory
    {
        get => (string?)GetValue(SelectedCategoryProperty);
        set => SetValue(SelectedCategoryProperty, value);
    }

    public DataTemplateSelector? ItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    private bool _suppressCategorySync;

    public SettingsListControl()
    {
        InitializeComponent();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SettingsListControl)d;
        ctrl.SettingsItemsControl.ItemsSource = (IEnumerable?)e.NewValue;
    }

    private static void OnCategoriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SettingsListControl)d;
        ctrl.CategoryListBox.ItemsSource = (IEnumerable?)e.NewValue;
    }

    private static void OnSelectedCategoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SettingsListControl)d;
        ctrl._suppressCategorySync = true;
        ctrl.CategoryListBox.SelectedItem = (string?)e.NewValue;
        ctrl._suppressCategorySync = false;
    }

    private static void OnTemplateSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SettingsListControl)d;
        ctrl.SettingsItemsControl.ItemTemplateSelector = (DataTemplateSelector?)e.NewValue;
    }

    public void ScrollToCategory(string category)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _suppressCategorySync = true;

            var items = SettingsItemsControl.ItemsSource as IList;
            if (items is null) return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is CategoryHeaderItem header && header.Name == category)
                {
                    var container = SettingsItemsControl.ItemContainerGenerator
                        .ContainerFromIndex(i) as FrameworkElement;
                    if (container is not null)
                    {
                        var transform = container.TransformToAncestor(SettingsScrollViewer);
                        var point = transform.Transform(new Point(0, 0));
                        SettingsScrollViewer.ScrollToVerticalOffset(
                            SettingsScrollViewer.VerticalOffset + point.Y);
                    }
                    break;
                }
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                _suppressCategorySync = false);
        });
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_suppressCategorySync) return;

        var items = SettingsItemsControl.ItemsSource as IList;
        if (items is null || items.Count == 0) return;

        _suppressCategorySync = true;
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var container = SettingsItemsControl.ItemContainerGenerator
                    .ContainerFromIndex(i) as FrameworkElement;
                if (container is null) continue;

                var transform = container.TransformToAncestor(SettingsScrollViewer);
                var point = transform.Transform(new Point(0, 0));
                if (point.Y >= -10)
                {
                    string? cat = null;
                    for (int j = i; j >= 0; j--)
                    {
                        if (items[j] is CategoryHeaderItem header)
                        {
                            cat = header.Name;
                            break;
                        }
                    }
                    if (cat is not null)
                        SetCurrentValue(SelectedCategoryProperty, cat);
                    break;
                }
            }
        }
        catch
        {
            // Layout not ready
        }
        finally
        {
            _suppressCategorySync = false;
        }
    }

    private void OnCategorySidebarSelection(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressCategorySync) return;
        if (CategoryListBox.SelectedItem is string category)
            ScrollToCategory(category);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchWatermark.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
