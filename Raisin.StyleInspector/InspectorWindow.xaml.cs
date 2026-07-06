using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Raisin.StyleInspector;

public partial class InspectorWindow : Window
{
    private readonly ObservableCollection<InspectedProperty> _properties = new();
    private readonly ObservableCollection<ComparedProperty> _compared = new();
    private ICollectionView? _inspectView;
    private ICollectionView? _compareView;
    private ElementPicker? _picker;
    private FrameworkElement? _elementA;
    private FrameworkElement? _elementB;
    private bool _pickingB;
    private bool _isCompareMode;
    private VisualTreeNode? _treeRoot;
    private DependencyObject? _lastTreeRootElement;
    private bool _suppressTreeSelection;
    private VisualTreeNode? _treeRootB;
    private DependencyObject? _lastTreeRootElementB;
    private bool _suppressTreeSelectionB;

    public InspectorWindow()
    {
        InitializeComponent();

        _inspectView = CollectionViewSource.GetDefaultView(_properties);
        _inspectView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        _inspectView.Filter = FilterInspectProperty;

        _compareView = CollectionViewSource.GetDefaultView(_compared);
        _compareView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        _compareView.Filter = FilterCompareProperty;

        SetInspectMode();
    }

    private void SetInspectMode()
    {
        _isCompareMode = false;
        PropertyList.ItemsSource = _inspectView;
        PropertyList.ItemContainerStyle = (Style)Resources["InspectorItemStyle"];
        PropertyList.ItemTemplate = (DataTemplate)Resources["InspectTemplate"];
        HideDefaultsBox.Visibility = Visibility.Visible;
        DiffsOnlyBox.Visibility = Visibility.Collapsed;
    }

    private void SetCompareMode()
    {
        _isCompareMode = true;
        PropertyList.ItemsSource = _compareView;
        PropertyList.ItemContainerStyle = (Style)Resources["CompareItemStyle"];
        PropertyList.ItemTemplate = (DataTemplate)Resources["CompareTemplate"];
        HideDefaultsBox.Visibility = Visibility.Collapsed;
        DiffsOnlyBox.Visibility = Visibility.Visible;
        ClearBButton.Visibility = Visibility.Visible;
    }

    private void OnPickClick(object sender, RoutedEventArgs e)
    {
        if (_picker?.IsPicking == true)
            StopPicking();
        else
            StartPicking(pickB: false);
    }

    private void OnPickBClick(object sender, RoutedEventArgs e)
    {
        if (_picker?.IsPicking == true)
            StopPicking();
        else
            StartPicking(pickB: true);
    }

    private void StartPicking(bool pickB)
    {
        _pickingB = pickB;
        _picker = new ElementPicker(OnElementPicked, OnElementHovered);
        _picker.Start();

        var activeButton = pickB ? PickBButton : PickButton;
        activeButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
        StatusText.Text = pickB ? "Click element B to compare…" : "Click an element to inspect…";
    }

    private void StopPicking()
    {
        _picker?.Stop();
        _picker = null;
        PickButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
        PickBButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
    }

    private void OnElementHovered(FrameworkElement element)
    {
        if (_pickingB)
            ShowElementB(element, live: true);
        else
            ShowElementA(element, live: true);
    }

    private void OnElementPicked(FrameworkElement element)
    {
        StopPicking();
        if (_pickingB)
            ShowElementB(element, live: false);
        else
            ShowElementA(element, live: false);
    }

    private void ShowElementA(FrameworkElement element, bool live)
    {
        _elementA = element;
        var name = string.IsNullOrEmpty(element.Name) ? "" : $" \"{element.Name}\"";
        ElementType.Text = $"{element.GetType().Name}{name}";
        ElementDetail.Text = element.GetType().FullName;
        ElementInfoPanel.Visibility = Visibility.Visible;

        BuildVisualTree(element);

        if (_elementB != null)
            RunCompare();
        else
            ShowInspectView(element);
    }

    private void ShowElementB(FrameworkElement element, bool live)
    {
        _elementB = element;
        var name = string.IsNullOrEmpty(element.Name) ? "" : $" \"{element.Name}\"";
        ElementBType.Text = $"{element.GetType().Name}{name}";
        ElementBDetail.Text = element.GetType().FullName;
        ElementBInfoPanel.Visibility = Visibility.Visible;

        BuildVisualTreeB(element);

        if (_elementA != null)
            RunCompare();
    }

    private void BuildVisualTree(DependencyObject element)
    {
        try
        {
            var root = VisualTreeWalker.FindAncestor(element, 5);

            _suppressTreeSelection = true;
            if (!ReferenceEquals(root, _lastTreeRootElement) || _treeRoot == null)
            {
                _lastTreeRootElement = root;
                _treeRoot = VisualTreeWalker.Build(root);

                if (!_treeRoot.HasChildren && !ReferenceEquals(root, element))
                {
                    _treeRoot = VisualTreeWalker.Build(element);
                    _lastTreeRootElement = element;
                }

                VisualTreeView.ItemsSource = new[] { _treeRoot };
                _treeRoot.IsExpanded = true;
            }

            ClearTreeSelection(_treeRoot);
            VisualTreeWalker.ExpandPathTo(_treeRoot, element);
            _suppressTreeSelection = false;

            TreePanel.Visibility = Visibility.Visible;
            TreeSplitter.Visibility = Visibility.Visible;
            if (TreeColumn.Width.Value < 10)
                TreeColumn.Width = new GridLength(240);
        }
        catch (Exception ex)
        {
            TreePanel.Visibility = Visibility.Collapsed;
            TreeSplitter.Visibility = Visibility.Collapsed;
            TreeColumn.Width = new GridLength(0);
            StatusText.Text = $"Tree: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static void ClearTreeSelection(VisualTreeNode node)
    {
        node.IsSelected = false;
        foreach (var child in node.Children)
            ClearTreeSelection(child);
    }

    private void OnTreeNodeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressTreeSelection) return;
        if (e.NewValue is VisualTreeNode node && node.Element is FrameworkElement fe)
        {
            _elementA = fe;
            var name = string.IsNullOrEmpty(fe.Name) ? "" : $" \"{fe.Name}\"";
            ElementType.Text = $"{fe.GetType().Name}{name}";
            ElementDetail.Text = fe.GetType().FullName;

            if (_elementB != null)
                RunCompare();
            else
                ShowInspectView(fe);
        }
    }

    private void OnTreeUpClick(object sender, RoutedEventArgs e)
    {
        if (_lastTreeRootElement == null) return;
        var parent = VisualTreeWalker.GetVisualParent(_lastTreeRootElement);
        if (parent == null)
        {
            StatusText.Text = "Already at visual tree root";
            return;
        }

        try
        {
            var selectedElement = _elementA;
            _suppressTreeSelection = true;
            _lastTreeRootElement = parent;
            _treeRoot = VisualTreeWalker.Build(parent);
            VisualTreeView.ItemsSource = new[] { _treeRoot };
            _treeRoot.IsExpanded = true;
            if (selectedElement != null)
                VisualTreeWalker.ExpandPathTo(_treeRoot, selectedElement);
            _suppressTreeSelection = false;
        }
        catch
        {
            _suppressTreeSelection = false;
            StatusText.Text = "Cannot navigate further up";
        }
    }

    private void OnTreeExpandAllClick(object sender, RoutedEventArgs e)
    {
        if (_elementA == null) return;

        try
        {
            var selectedElement = _elementA;
            _suppressTreeSelection = true;
            var root = VisualTreeWalker.FindVisualRoot(selectedElement);
            _lastTreeRootElement = root;
            _treeRoot = VisualTreeWalker.Build(root);
            VisualTreeView.ItemsSource = new[] { _treeRoot };
            _treeRoot.IsExpanded = true;
            VisualTreeWalker.ExpandPathTo(_treeRoot, selectedElement);
            _suppressTreeSelection = false;
        }
        catch
        {
            _suppressTreeSelection = false;
            StatusText.Text = "Cannot expand full tree";
        }
    }

    private void BuildVisualTreeB(DependencyObject element)
    {
        try
        {
            var root = VisualTreeWalker.FindAncestor(element, 5);

            _suppressTreeSelectionB = true;
            if (!ReferenceEquals(root, _lastTreeRootElementB) || _treeRootB == null)
            {
                _lastTreeRootElementB = root;
                _treeRootB = VisualTreeWalker.Build(root);

                if (!_treeRootB.HasChildren && !ReferenceEquals(root, element))
                {
                    _treeRootB = VisualTreeWalker.Build(element);
                    _lastTreeRootElementB = element;
                }

                VisualTreeBView.ItemsSource = new[] { _treeRootB };
                _treeRootB.IsExpanded = true;
            }

            ClearTreeSelection(_treeRootB);
            VisualTreeWalker.ExpandPathTo(_treeRootB, element);
            _suppressTreeSelectionB = false;

            TreeBPanel.Visibility = Visibility.Visible;
            TreeBSplitter.Visibility = Visibility.Visible;
            if (TreeBColumn.Width.Value < 10)
                TreeBColumn.Width = new GridLength(240);
        }
        catch (Exception ex)
        {
            TreeBPanel.Visibility = Visibility.Collapsed;
            TreeBSplitter.Visibility = Visibility.Collapsed;
            TreeBColumn.Width = new GridLength(0);
            StatusText.Text = $"Tree B: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void OnTreeBNodeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressTreeSelectionB) return;
        if (e.NewValue is VisualTreeNode node && node.Element is FrameworkElement fe)
        {
            _elementB = fe;
            var name = string.IsNullOrEmpty(fe.Name) ? "" : $" \"{fe.Name}\"";
            ElementBType.Text = $"{fe.GetType().Name}{name}";
            ElementBDetail.Text = fe.GetType().FullName;

            if (_elementA != null)
                RunCompare();
        }
    }

    private void OnTreeBUpClick(object sender, RoutedEventArgs e)
    {
        if (_lastTreeRootElementB == null) return;
        var parent = VisualTreeWalker.GetVisualParent(_lastTreeRootElementB);
        if (parent == null)
        {
            StatusText.Text = "Already at visual tree root";
            return;
        }

        try
        {
            var selectedElement = _elementB;
            _suppressTreeSelectionB = true;
            _lastTreeRootElementB = parent;
            _treeRootB = VisualTreeWalker.Build(parent);
            VisualTreeBView.ItemsSource = new[] { _treeRootB };
            _treeRootB.IsExpanded = true;
            if (selectedElement != null)
                VisualTreeWalker.ExpandPathTo(_treeRootB, selectedElement);
            _suppressTreeSelectionB = false;
        }
        catch
        {
            _suppressTreeSelectionB = false;
            StatusText.Text = "Cannot navigate further up";
        }
    }

    private void OnTreeBExpandAllClick(object sender, RoutedEventArgs e)
    {
        if (_elementB == null) return;

        try
        {
            var selectedElement = _elementB;
            _suppressTreeSelectionB = true;
            var root = VisualTreeWalker.FindVisualRoot(selectedElement);
            _lastTreeRootElementB = root;
            _treeRootB = VisualTreeWalker.Build(root);
            VisualTreeBView.ItemsSource = new[] { _treeRootB };
            _treeRootB.IsExpanded = true;
            VisualTreeWalker.ExpandPathTo(_treeRootB, selectedElement);
            _suppressTreeSelectionB = false;
        }
        catch
        {
            _suppressTreeSelectionB = false;
            StatusText.Text = "Cannot expand full tree";
        }
    }

    private void ShowInspectView(FrameworkElement element)
    {
        if (_isCompareMode)
            SetInspectMode();

        _properties.Clear();
        foreach (var prop in PropertyEnumerator.Enumerate(element))
            _properties.Add(prop);

        PopulateStyleOrigins(element);
        ShowStyleChain(element);
        UpdateStatus();
        UpdateResetAllVisibility();
        ExportButton.Visibility = Visibility.Visible;
    }

    private void PopulateStyleOrigins(FrameworkElement element)
    {
        var origins = StyleChainResolver.ResolveSetterOrigins(element);
        foreach (var prop in _properties)
        {
            if (!origins.TryGetValue(prop.Name, out var setters) || setters.Count == 0)
                continue;

            prop.StyleOrigin = setters[0].StyleLabel;
            var lines = new List<string>(setters.Count);
            for (int i = 0; i < setters.Count; i++)
            {
                var s = setters[i];
                var dict = s.DictionarySource != null ? $"  ({s.DictionarySource})" : "";
                var suffix = i > 0 ? "  — overridden" : "";
                lines.Add($"{s.StyleLabel} = {s.DisplayValue}{dict}{suffix}");
            }
            prop.OriginDetail = string.Join("\n", lines);
        }
    }

    private void RunCompare()
    {
        if (_elementA == null || _elementB == null) return;

        if (!_isCompareMode)
            SetCompareMode();

        _compared.Clear();
        foreach (var cp in CompareEngine.Compare(_elementA, _elementB))
            _compared.Add(cp);

        StyleChainPanel.Visibility = Visibility.Collapsed;
        ExportButton.Visibility = Visibility.Visible;
        UpdateStatus();
    }

    private void ShowStyleChain(FrameworkElement element)
    {
        var chain = StyleChainResolver.Resolve(element);
        StyleChainList.ItemsSource = chain;
        StyleChainPanel.Visibility = chain.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnStyleChainEntryClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is StyleChainEntry entry && entry.Style != null)
        {
            var setters = StyleChainResolver.GetSetterNames(entry.Style);
            if (setters.Count > 0)
            {
                SearchBox.Text = "";
                HideDefaultsBox.IsChecked = false;

                var setterSet = new HashSet<string>(setters, StringComparer.OrdinalIgnoreCase);
                _inspectView!.Filter = obj =>
                    obj is InspectedProperty prop && setterSet.Contains(prop.Name);
                _inspectView.Refresh();
                UpdateStatus();
                StatusText.Text = $"Showing {setters.Count} properties from \"{entry.Label}\"";
            }
        }
    }

    private void OnClearBClick(object sender, RoutedEventArgs e)
    {
        _elementB = null;
        _treeRootB = null;
        _lastTreeRootElementB = null;
        ElementBInfoPanel.Visibility = Visibility.Collapsed;
        ClearBButton.Visibility = Visibility.Collapsed;
        DiffsOnlyBox.Visibility = Visibility.Collapsed;
        TreeBPanel.Visibility = Visibility.Collapsed;
        TreeBSplitter.Visibility = Visibility.Collapsed;
        TreeBColumn.Width = new GridLength(0);

        if (_elementA != null)
            ShowInspectView(_elementA);
        else
            SetInspectMode();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshFilter();
    private void OnFilterChanged(object sender, RoutedEventArgs e) => RefreshFilter();

    private void RefreshFilter()
    {
        if (_isCompareMode)
        {
            _compareView!.Filter = FilterCompareProperty;
            _compareView.Refresh();
        }
        else
        {
            _inspectView!.Filter = FilterInspectProperty;
            _inspectView.Refresh();
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_isCompareMode)
        {
            var total = _compared.Count;
            var visible = _compareView?.Cast<object>().Count() ?? total;
            var diffs = _compared.Count(p => p.IsDifferent);
            StatusText.Text = $"{diffs} differences, {visible} of {total} properties";
        }
        else
        {
            var total = _properties.Count;
            var visible = _inspectView?.Cast<object>().Count() ?? total;
            StatusText.Text = visible == total
                ? $"{total} properties"
                : $"{visible} of {total} properties";
        }
    }

    private bool FilterInspectProperty(object obj)
    {
        if (obj is not InspectedProperty prop) return false;
        if (HideDefaultsBox.IsChecked == true && prop.IsDefault) return false;

        var search = SearchBox.Text;
        if (!string.IsNullOrEmpty(search))
            return prop.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private bool FilterCompareProperty(object obj)
    {
        if (obj is not ComparedProperty prop) return false;
        if (DiffsOnlyBox.IsChecked == true && prop.IsMatch) return false;

        var search = SearchBox.Text;
        if (!string.IsNullOrEmpty(search))
            return prop.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        string xaml;
        if (_isCompareMode && _elementA != null && _elementB != null)
        {
            xaml = XamlExporter.ExportDiff(_elementA, _elementB, _compared);
        }
        else if (_elementA != null)
        {
            var hasEdits = _properties.Any(p => p.IsEdited);
            xaml = XamlExporter.ExportStyle(_elementA, _properties, editedOnly: hasEdits);
        }
        else return;

        Clipboard.SetText(xaml);
        StatusText.Text = "XAML copied to clipboard";
    }

    private void OnCopySetterClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is InspectedProperty prop)
        {
            Clipboard.SetText(XamlExporter.ExportSetter(prop));
            StatusText.Text = $"Copied setter for {prop.Name}";
        }
    }

    private void OnCopyValueClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is InspectedProperty prop)
        {
            Clipboard.SetText(prop.DisplayValue);
            StatusText.Text = $"Copied value of {prop.Name}";
        }
    }

    private void OnCopyChainClick(object sender, RoutedEventArgs e)
    {
        if (StyleChainList.ItemsSource is not List<StyleChainEntry> chain) return;
        var sb = new StringBuilder();
        foreach (var entry in chain)
            sb.AppendLine($"{entry.Label} — {entry.Detail}");
        Clipboard.SetText(sb.ToString());
        StatusText.Text = "Style chain copied to clipboard";
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
