using System.Windows;

namespace Raisin.WPF.Base;

public static class WindowPlacementHelper
{
    public static WindowPlacement? FromNullable(
        double? left, double? top, double? width, double? height, bool maximized)
    {
        if (width is null || height is null) return null;
        return new(left ?? 0, top ?? 0, width.Value, height.Value, maximized);
    }

    public static WindowPlacement Capture(Window window)
    {
        var bounds = window.WindowState != WindowState.Normal
            ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);
        return new(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            window.WindowState == WindowState.Maximized);
    }

    public static void Restore(Window window, WindowPlacement p)
    {
        if (IsVisibleOnScreen(p.Left, p.Top, p.Width, p.Height))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = p.Left;
            window.Top = p.Top;
            window.Width = p.Width;
            window.Height = p.Height;
        }
        if (p.Maximized)
            window.WindowState = WindowState.Maximized;
    }

    public static bool IsVisibleOnScreen(double left, double top, double width, double height)
    {
        double vsLeft = SystemParameters.VirtualScreenLeft;
        double vsTop = SystemParameters.VirtualScreenTop;
        double vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
        double vsBottom = vsTop + SystemParameters.VirtualScreenHeight;
        double overlapX = Math.Max(0, Math.Min(left + width, vsRight) - Math.Max(left, vsLeft));
        double overlapY = Math.Max(0, Math.Min(top + height, vsBottom) - Math.Max(top, vsTop));
        return overlapX >= 100 && overlapY >= 50;
    }
}
