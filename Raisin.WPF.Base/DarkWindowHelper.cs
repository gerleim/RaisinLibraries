using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Raisin.WPF.Base;

public static class DarkWindowHelper
{
    private const int WM_ERASEBKGND = 0x0014;
    private const uint FillColor = 0x00302D2D; // BGR for #2D2D30

    private sealed class WindowInfo
    {
        public int CaptionPx;
    }

    private static readonly ConcurrentDictionary<IntPtr, WindowInfo> _windows = new();

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var chrome = WindowChrome.GetWindowChrome(window);

        if (chrome != null || window.WindowStyle == WindowStyle.None)
        {
            ApplyFloating(window);
            return;
        }

        var source = PresentationSource.FromVisual(window);
        double dpiScale = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        var info = new WindowInfo();
        info.CaptionPx = (int)(32 * dpiScale);
        _windows[hwnd] = info;

        var brush = NativeMethods.CreateSolidBrush(FillColor);
        NativeMethods.SetClassLongPtr(hwnd, NativeMethods.GCLP_HBRBACKGROUND, brush);

        var hwndSource = HwndSource.FromHwnd(hwnd);
        if (hwndSource?.CompositionTarget != null)
            hwndSource.CompositionTarget.BackgroundColor = Color.FromRgb(0x2D, 0x2D, 0x30);

        hwndSource?.AddHook(WndProc);

        uint darkMode = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(uint));
        uint captionColor = 0x00302D2D;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(uint));
        uint cornerPreference = 1; // DWMWCP_DONOTROUND
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(uint));
    }

    public static void ApplyFloating(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        uint darkMode = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(uint));
        uint captionColor = FillColor;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(uint));
        uint borderColor = FillColor;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref borderColor, sizeof(uint));

        var hwndSource = HwndSource.FromHwnd(hwnd);
        hwndSource?.AddHook(FloatingWndProc);
    }

    private const int WM_ENTERSIZEMOVE = 0x0231;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const int WM_SIZE = 0x0005;

    [ThreadStatic] private static bool _inSizeMove;

    private static IntPtr FloatingWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_ENTERSIZEMOVE:
                _inSizeMove = true;
                break;
            case WM_EXITSIZEMOVE:
                _inSizeMove = false;
                break;
            case WM_SIZE when _inSizeMove:
                NativeMethods.DwmFlush();
                break;
        }
        return IntPtr.Zero;
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_ERASEBKGND)
            return IntPtr.Zero;
        if (!_windows.TryGetValue(hwnd, out var info))
            return IntPtr.Zero;

        var hdc = wParam;
        var rect = new NativeMethods.RECT();
        NativeMethods.GetClientRect(hwnd, ref rect);
        if (info.CaptionPx > 0)
            rect.Top = info.CaptionPx;
        var brush = NativeMethods.CreateSolidBrush(FillColor);
        NativeMethods.FillRect(hdc, ref rect, brush);
        NativeMethods.DeleteObject(brush);
        handled = true;
        return (IntPtr)1;
    }
}
