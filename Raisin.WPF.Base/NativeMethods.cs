using System.Runtime.InteropServices;

namespace Raisin.WPF.Base;

internal static class NativeMethods
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref uint attrValue, int attrSize);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
    public static extern nint SetClassLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(nint hWnd, ref RECT lpRect);

    [DllImport("user32.dll")]
    public static extern int FillRect(nint hDC, ref RECT lprc, nint hbr);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll")]
    public static extern nint CreateSolidBrush(uint crColor);

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_CAPTION_COLOR = 35;
    public const int GCLP_HBRBACKGROUND = -10;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}
