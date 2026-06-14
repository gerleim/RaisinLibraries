using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Raisin.WPF.Base;

/// <summary>
/// Applies dark title bar, dark background brush, and WM_ERASEBKGND hook
/// to prevent white flash. Call from OnSourceInitialized.
/// </summary>
public static class DarkWindowHelper
{
    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        // Override the Win32 window-class background brush so the OS paints
        // the client area dark before WPF renders its first frame.
        var brush = NativeMethods.CreateSolidBrush(0x00212121);
        NativeMethods.SetClassLongPtr(hwnd, NativeMethods.GCLP_HBRBACKGROUND, brush);

        // Tell WPF's DirectX composition layer to clear to dark instead of white.
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget != null)
            source.CompositionTarget.BackgroundColor = Color.FromRgb(0x21, 0x21, 0x21);

        // Intercept WM_ERASEBKGND so the OS paints dark instead of white
        // during resize and before WPF renders.
        source?.AddHook(WndProc);

        // DWM dark mode + caption color
        uint darkMode = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(uint));
        uint captionColor = 0x00302D2D;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(uint));
    }

    private const int WM_ERASEBKGND = 0x0014;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_ERASEBKGND)
        {
            var hdc = wParam;
            var rect = new NativeMethods.RECT();
            NativeMethods.GetClientRect(hwnd, ref rect);
            var brush = NativeMethods.CreateSolidBrush(0x00212121);
            NativeMethods.FillRect(hdc, ref rect, brush);
            NativeMethods.DeleteObject(brush);
            handled = true;
            return (IntPtr)1;
        }
        return IntPtr.Zero;
    }
}
