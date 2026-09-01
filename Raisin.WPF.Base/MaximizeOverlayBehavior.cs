using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using AvalonDock;
using AvalonDock.Layout;

namespace Raisin.WPF.Base;

public class MaximizeOverlayBehavior : IMaximizeHost
{
    private readonly DockingManager _dockingManager;
    private readonly Window _window;
    private readonly Grid _overlay;
    private readonly Border _overlayBorder;
    private readonly ContentControl _overlayContent;
    private MaximizedViewState? _state;

    /// <summary>Maximizes the active pane and restores it again - "fullscreen".</summary>
    private const Key MaximizeKey = Key.F;

    private class MaximizedViewState
    {
        public required ToolWindowViewModel ViewModel { get; init; }
        public required FrameworkElement View { get; init; }
        public required ContentPresenter ContentPresenter { get; init; }
        public required DataTemplate? OriginalTemplate { get; init; }
        public required DataTemplateSelector? OriginalTemplateSelector { get; init; }
        public Window? OverlayWindow { get; init; }
    }

    private static readonly DataTemplate PlaceholderTemplate = CreatePlaceholderTemplate();

    private static DataTemplate CreatePlaceholderTemplate()
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(TextBlock.TextProperty, "View is maximized");
        factory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)));
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(TextBlock.FontSizeProperty, 14.0);
        template.VisualTree = factory;
        template.Seal();
        return template;
    }

    // --- Attached property: set on a Window to wire up the behavior ---

    public static readonly DependencyProperty DockingManagerProperty =
        DependencyProperty.RegisterAttached(
            "DockingManager",
            typeof(DockingManager),
            typeof(MaximizeOverlayBehavior),
            new PropertyMetadata(null, OnDockingManagerChanged));

    public static DockingManager? GetDockingManager(DependencyObject obj)
        => (DockingManager?)obj.GetValue(DockingManagerProperty);

    public static void SetDockingManager(DependencyObject obj, DockingManager? value)
        => obj.SetValue(DockingManagerProperty, value);

    // --- Attached read-only property: retrieve the behavior instance ---

    private static readonly DependencyPropertyKey InstancePropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "Instance",
            typeof(MaximizeOverlayBehavior),
            typeof(MaximizeOverlayBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty InstanceProperty = InstancePropertyKey.DependencyProperty;

    public static MaximizeOverlayBehavior? GetInstance(DependencyObject obj)
        => (MaximizeOverlayBehavior?)obj.GetValue(InstanceProperty);

    public static IMaximizeHost? FindHost(DependencyObject element)
    {
        var window = Window.GetWindow(element);
        if (window is not null)
        {
            var instance = GetInstance(window);
            if (instance is not null) return instance;
        }
        if (Application.Current?.MainWindow is Window main)
            return GetInstance(main);
        return null;
    }

    private static void OnDockingManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window || e.NewValue is not DockingManager dm) return;

        var behavior = new MaximizeOverlayBehavior(dm, window);
        window.SetValue(InstancePropertyKey, behavior);
    }

    private MaximizeOverlayBehavior(DockingManager dockingManager, Window window)
    {
        _dockingManager = dockingManager;
        _window = window;

        _overlayContent = new ContentControl();
        _overlayBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = _overlayContent,
        };
        _overlayBorder.MouseLeftButtonDown += (_, e) => e.Handled = true;

        _overlay = new Grid
        {
            Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0)),
        };
        _overlay.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource == _overlay)
                RestoreMaximizedView();
        };
        _overlay.Children.Add(_overlayBorder);

        InjectOverlay();

        window.PreviewKeyDown += OnPreviewKeyDown;
        window.Closing += OnClosing;

        // Undocked panes live in their own windows, so the hotkey has to be hooked there too.
        _dockingManager.LayoutFloatingWindowControlCreated += (_, e) =>
            e.LayoutFloatingWindowControl.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void InjectOverlay()
    {
        var existingContent = _window.Content as UIElement;
        if (existingContent is null) return;

        _window.Content = null;
        var wrapper = new Grid();
        wrapper.Children.Add(existingContent);
        wrapper.Children.Add(_overlay);
        _window.Content = wrapper;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape && _state is not null)
        {
            e.Handled = true;
            RestoreMaximizedView();
            return;
        }

        if (key == MaximizeKey && Keyboard.Modifiers == ModifierKeys.None && !IsTypingFocus())
            e.Handled = ToggleMaximizeFor(sender as Window);
    }

    /// <summary>
    /// Maximizes the active pane, or restores the maximized one. Returns false when the key
    /// means nothing here - the active pane offers no maximize icon, or it sits in another
    /// window - so the keystroke carries on to whoever else wants it.
    /// </summary>
    private bool ToggleMaximizeFor(Window? sourceWindow)
    {
        if (_state is not null)
        {
            RestoreMaximizedView();
            return true;
        }

        if (_dockingManager.ActiveContent is not ToolWindowViewModel vm) return false;

        var view = FindView(vm);
        if (view is null) return false;

        // Only the window the key was typed in gets to act on it.
        if (sourceWindow is not null && Window.GetWindow(view) != sourceWindow) return false;

        var button = FindMaximizeButton(view);
        if (button is null || !button.IsVisible || !button.IsEnabled) return false;

        button.Toggle();
        return true;
    }

    private FrameworkElement? FindView(ToolWindowViewModel vm)
    {
        var found = FindView(_dockingManager, vm);
        if (found is not null) return found;

        foreach (var fw in _dockingManager.FloatingWindows)
        {
            found = FindView(fw, vm);
            if (found is not null) return found;
        }
        return null;
    }

    private static FrameworkElement? FindView(DependencyObject root, ToolWindowViewModel vm)
    {
        if (root is UserControl uc && ReferenceEquals(uc.DataContext, vm)) return uc;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindView(VisualTreeHelper.GetChild(root, i), vm);
            if (found is not null) return found;
        }
        return null;
    }

    private static MaximizeButton? FindMaximizeButton(DependencyObject root)
    {
        if (root is MaximizeButton button) return button;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindMaximizeButton(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// True while the keystroke belongs to whatever has focus, i.e. text entry.
    /// </summary>
    private static bool IsTypingFocus()
    {
        for (DependencyObject? d = Keyboard.FocusedElement as DependencyObject; d is not null;
             d = d is Visual or Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            switch (d)
            {
                case TextBoxBase:
                case PasswordBox:
                case ComboBox { IsEditable: true }:
                    return true;
            }
        }
        return false;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_state is not null)
            RestoreMaximizedView();
    }

    // --- IMaximizeHost ---

    public bool IsAnyViewMaximized => _state is not null;

    public bool MaximizeView(ToolWindowViewModel vm, FrameworkElement view)
    {
        if (_state is not null) return false;

        var hostWindow = FindFloatingWindowFor(vm) ?? _window;

        if (VisualTreeHelper.GetParent(view) is not ContentPresenter cp)
            return false;

        var savedTemplate = cp.ContentTemplate;
        var savedSelector = cp.ContentTemplateSelector;

        cp.ContentTemplateSelector = null;
        cp.ContentTemplate = PlaceholderTemplate;

        view.DataContext = vm;

        Window? overlayWindow = null;
        if (hostWindow != _window)
        {
            overlayWindow = CreateFloatingOverlay(hostWindow, view);
            overlayWindow.Show();
        }
        else
        {
            _overlayContent.Content = view;
            _overlayBorder.Width = _window.ActualWidth * 0.9;
            _overlayBorder.Height = _window.ActualHeight * 0.9;
            _overlay.Visibility = Visibility.Visible;
        }

        _state = new MaximizedViewState
        {
            ViewModel = vm,
            View = view,
            ContentPresenter = cp,
            OriginalTemplate = savedTemplate,
            OriginalTemplateSelector = savedSelector,
            OverlayWindow = overlayWindow,
        };

        vm.IsMaximizedOverlay = true;
        return true;
    }

    public void RestoreMaximizedView()
    {
        if (_state is null) return;
        var state = _state;
        _state = null;

        if (state.OverlayWindow is not null)
        {
            if (state.OverlayWindow.Content is Grid g)
            {
                if (g.Children[0] is Border b) b.Child = null;
                g.Children.Clear();
            }
            state.OverlayWindow.Content = null;
            state.OverlayWindow.Close();
        }
        else
        {
            _overlayContent.Content = null;
            _overlay.Visibility = Visibility.Collapsed;
        }

        state.ContentPresenter.ContentTemplateSelector = state.OriginalTemplateSelector;
        state.ContentPresenter.ContentTemplate = state.OriginalTemplate;
        state.ViewModel.IsMaximizedOverlay = false;
    }

    private Window? FindFloatingWindowFor(ToolWindowViewModel vm)
    {
        foreach (var fw in _dockingManager.FloatingWindows)
        {
            if (fw.Model is ILayoutContainer container
                && container.Descendents().OfType<LayoutContent>().Any(c => c.Content == vm))
                return fw;
        }
        return null;
    }

    /// <summary>
    /// Where a window actually is on screen, in device-independent units.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>: while a window is maximized
    /// those still report its <i>restore</i> bounds, so anything positioned from them lands where the
    /// window would be if it were un-maximized. It only ever looked right because a floating window is
    /// usually not maximized.
    /// <para>
    /// The rendered size and the on-screen origin are correct in both states. The origin comes back in
    /// device pixels, so it is transformed to match the units the Window properties are set in —
    /// without that the overlay drifts on any display not at 100% scaling.
    /// </para>
    /// </remarks>
    private static Rect GetScreenBounds(Window window)
    {
        var origin = new Point(window.Left, window.Top);

        if (PresentationSource.FromVisual(window) is { } source)
            origin = source.CompositionTarget.TransformFromDevice
                .Transform(window.PointToScreen(new Point(0, 0)));

        return new Rect(origin, new Size(window.ActualWidth, window.ActualHeight));
    }

    private Window CreateFloatingOverlay(Window hostWindow, FrameworkElement view)
    {
        var bounds = GetScreenBounds(hostWindow);

        var overlay = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Owner = hostWindow,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
        };

        var border = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Width = bounds.Width * 0.9,
            Height = bounds.Height * 0.9,
            Child = view,
        };
        border.MouseLeftButtonDown += (_, e) => e.Handled = true;

        var backdrop = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0)),
        };
        backdrop.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource == backdrop)
                RestoreMaximizedView();
        };
        backdrop.Children.Add(border);
        overlay.Content = backdrop;

        overlay.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape
                || (key == MaximizeKey && Keyboard.Modifiers == ModifierKeys.None && !IsTypingFocus()))
            {
                e.Handled = true;
                RestoreMaximizedView();
            }
        };

        overlay.Closed += (_, _) =>
        {
            if (_state?.OverlayWindow == overlay)
                RestoreMaximizedView();
        };

        return overlay;
    }
}
