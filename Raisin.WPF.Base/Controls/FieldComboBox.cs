using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Raisin.WPF.Base.Controls;

/// <summary>
/// ComboBox that fixes WPF's "two-click problem": when a dropdown is open
/// and you click another control, the first click only closes the dropdown
/// (swallowed by mouse capture) and the target control requires a second click.
/// This hooks PreviewMouseDownOutsideCapturedElement to capture the click
/// position while the mouse is still down, then re-dispatches to the target.
/// </summary>
public class FieldComboBox : ComboBox
{
    public FieldComboBox()
    {
        AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent,
            new MouseButtonEventHandler(OnClickOutside), true);
    }

    private void OnClickOutside(object sender, MouseButtonEventArgs e)
    {
        if (!IsDropDownOpen) return;

        var window = Window.GetWindow(this);
        if (window == null) return;

        var pos = e.GetPosition(window);

        IsDropDownOpen = false;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var hit = VisualTreeHelper.HitTest(window, pos);
            if (hit?.VisualHit is not UIElement target) return;
            if (IsAncestorOf(target)) return;

            var timestamp = Environment.TickCount;

            target.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice, timestamp, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });

            target.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice, timestamp, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        });
    }
}
