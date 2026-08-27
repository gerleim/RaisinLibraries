using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace Raisin.WPF.Base;

public class ModalWindowBehavior : Behavior<Window>
{
    private static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(ModalWindowBehavior),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window) return;
        if (!(bool)e.NewValue) return;

        var behavior = new ModalWindowBehavior();
        Interaction.GetBehaviors(window).Add(behavior);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Activated += Window_Activated;
        AssociatedObject.Deactivated += Window_Deactivated;
        ModalWindowManager.Instance.PropertyChanged += Manager_PropertyChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Activated -= Window_Activated;
        AssociatedObject.Deactivated -= Window_Deactivated;
        ModalWindowManager.Instance.PropertyChanged -= Manager_PropertyChanged;
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        UpdateEnabledState();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        UpdateEnabledState();
    }

    private void Manager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModalWindowManager.IsCustomModalOpen) or nameof(ModalWindowManager.CurrentModalWindow))
        {
            UpdateEnabledState();
        }
    }

    private void UpdateEnabledState()
    {
        var manager = ModalWindowManager.Instance;
        if (!manager.IsCustomModalOpen)
        {
            AssociatedObject.IsEnabled = true;
            return;
        }

        var isThisWindowTheModal = manager.IsCurrentModal(AssociatedObject);
        AssociatedObject.IsEnabled = isThisWindowTheModal;
    }
}
