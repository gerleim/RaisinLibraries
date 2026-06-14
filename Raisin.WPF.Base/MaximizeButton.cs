using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Raisin.WPF.Base;

public class MaximizeButton : Button
{
    private readonly PackIcon _icon = new() { Kind = PackIconKind.Fullscreen, Width = 16, Height = 16 };

    public MaximizeButton()
    {
        Content = _icon;
        ToolTip = "Maximize";
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) =>
        {
            if (DataContext is ToolWindowViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ToolWindowViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is ToolWindowViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateState(newVm.IsMaximizedOverlay);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolWindowViewModel.IsMaximizedOverlay)
            && sender is ToolWindowViewModel vm)
            UpdateState(vm.IsMaximizedOverlay);
    }

    private void UpdateState(bool isMaximized)
    {
        _icon.Kind = isMaximized ? PackIconKind.FullscreenExit : PackIconKind.Fullscreen;
        ToolTip = isMaximized ? "Restore" : "Maximize";
    }

    protected override void OnClick()
    {
        base.OnClick();
        var view = FindAncestorView();
        if (view is null || view.DataContext is not ToolWindowViewModel vm) return;
        var host = MaximizeOverlayBehavior.FindHost(this);
        if (host is null) return;

        if (vm.IsMaximizedOverlay)
            host.RestoreMaximizedView();
        else
            host.MaximizeView(vm, view);
    }

    private FrameworkElement? FindAncestorView()
    {
        DependencyObject? current = VisualTreeHelper.GetParent(this);
        while (current is not null)
        {
            if (current is UserControl uc)
                return uc;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
