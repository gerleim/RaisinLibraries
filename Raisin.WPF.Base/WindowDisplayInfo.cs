using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Raisin.WPF.Base;

/// <summary>
/// Tracks the refresh rate of the display a top-level window occupies, and reports when it
/// changes because the window moved or the display configuration did.
/// </summary>
/// <remarks>
/// WPF does not pace a window to the panel it occupies. Measured with an app started on a
/// 280Hz display and its window dragged to a 60Hz one: composition frames kept arriving at 320
/// to 357 a second, so a control repainting once per composed frame drew nearly five frames for
/// every one that panel could show. Anything pacing itself to the display therefore has to ask
/// which display, and keep asking.
///
/// One instance per top-level window, so an app with floating windows gets one for each, and a
/// window dragged between monitors updates only its own. The value is pushed to whoever cares
/// through <see cref="Changed"/>, rather than polled: the rate changes rarely and discretely,
/// and a consumer polling it at the start of an animation both does work at the moment latency
/// matters and misses a window moved mid-animation.
/// </remarks>
public sealed class WindowDisplayInfo
{
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int ENUM_CURRENT_SETTINGS = -1;

    private static readonly ConditionalWeakTable<HwndSource, WindowDisplayInfo> Instances = new();
    private static Raisin.EventSystem.EventSystem? _events;

    private readonly HwndSource _source;
    private long _monitorSetHash;

    /// <summary>Refresh rate in hertz of the fastest display the window touches.</summary>
    /// <remarks>
    /// The fastest, not the one holding most of the window. A window straddling a 60Hz and a
    /// 280Hz panel is presented on both, and pacing to the slower one visibly degrades the half
    /// on the faster one - which is the half someone dragging a window between monitors is
    /// usually watching. Overshooting on the slow half only wastes work.
    /// </remarks>
    public int RefreshRate { get; private set; } = 60;

    /// <summary>Seconds per refresh: the reciprocal of <see cref="RefreshRate"/>.</summary>
    public double Period => RefreshRate > 0 ? 1.0 / RefreshRate : 0;

    /// <summary>Device names of every display the window touches, for diagnostics.</summary>
    /// <remarks>
    /// The name matters as much as the rate. A rate alone cannot show that a window has moved,
    /// because the failure this exists to correct produces the old rate on the new monitor,
    /// which is indistinguishable from not having moved at all.
    /// </remarks>
    public string Devices { get; private set; } = string.Empty;

    /// <summary>Raised on the UI thread when the window's display or its rate changes.</summary>
    public event Action<WindowDisplayInfo>? Changed;

    /// <summary>
    /// Lets a host receive <see cref="DisplayChangedArgs"/> on its event system. Optional: the
    /// bus is for announcement, never for delivering the value to a control that needs it.
    /// </summary>
    public static void Initialize(Raisin.EventSystem.EventSystem eventSystem) => _events = eventSystem;

    /// <summary>
    /// The instance for the top-level window <paramref name="visual"/> belongs to, created on
    /// first use; null if the visual is not attached to a window yet.
    /// </summary>
    public static WindowDisplayInfo? For(Visual visual)
    {
        if (PresentationSource.FromVisual(visual) is not HwndSource source || source.Handle == IntPtr.Zero)
            return null;

        return Instances.GetValue(source, s => new WindowDisplayInfo(s));
    }

    private WindowDisplayInfo(HwndSource source)
    {
        _source = source;
        source.AddHook(WndProc);
        source.Disposed += (_, _) => source.RemoveHook(WndProc);
        Update(raise: false);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WM_WINDOWPOSCHANGED is the only message that catches a window being dragged to
        // another monitor. WM_DPICHANGED only fires when the two displays scale differently,
        // so two panels at 100% produce nothing; WM_DISPLAYCHANGE is about the configuration
        // changing, not the window moving - it is handled too, for a refresh rate altered in
        // display settings while the app runs.
        if (msg == WM_WINDOWPOSCHANGED || msg == WM_DISPLAYCHANGE)
            Update(raise: true);

        return IntPtr.Zero;
    }

    private void Update(bool raise)
    {
        if (!GetWindowRect(_source.Handle, out RECT window))
            return;

        var monitors = IntersectingMonitors(window);

        // Which monitors the window touches is recomputed on every move, which is cheap:
        // enumerating monitors and reading their bounds is a handful of calls with no mode
        // query. Only when that set actually changes is EnumDisplaySettings asked for rates,
        // so dragging a window around one monitor costs almost nothing.
        long hash = 17;
        foreach (var m in monitors)
            hash = hash * 31 + m.ToInt64();

        if (hash == _monitorSetHash && RefreshRate > 0)
            return;

        _monitorSetHash = hash;

        int fastest = 0;
        var names = new List<string>(monitors.Count);
        foreach (var monitor in monitors)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
            if (!GetMonitorInfo(monitor, ref info))
                continue;

            names.Add(info.szDevice);

            var mode = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            if (EnumDisplaySettings(info.szDevice, ENUM_CURRENT_SETTINGS, ref mode)
                && mode.dmDisplayFrequency > fastest)
                fastest = mode.dmDisplayFrequency;
        }

        int previous = RefreshRate;
        RefreshRate = fastest > 0 ? fastest : 60;
        Devices = names.Count > 0 ? string.Join(", ", names) : string.Empty;

        if (!raise)
            return;

        Changed?.Invoke(this);

        // Announced for observers - diagnostics, logging, anything that wants to know. The
        // controls that need the value hold this object and read it; the bus is not their
        // transport.
        if (previous != RefreshRate)
            _events?.Invoke(this, new DisplayChangedArgs(_source.Handle, Devices, RefreshRate));
    }

    private static List<IntPtr> IntersectingMonitors(RECT window)
    {
        var all = new List<IntPtr>(4);
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr monitor, IntPtr _, ref RECT bounds, IntPtr _) =>
            {
                if (bounds.Left < window.Right && bounds.Right > window.Left
                    && bounds.Top < window.Bottom && bounds.Bottom > window.Top)
                    all.Add(monitor);
                return true;
            }, IntPtr.Zero);

        // A window dragged entirely off-screen, or minimised to an empty rect, intersects
        // nothing. Falling back to the nearest monitor keeps a usable rate rather than none.
        if (all.Count == 0)
        {
            IntPtr nearest = MonitorFromRect(ref window, MONITOR_DEFAULTTONEAREST);
            if (nearest != IntPtr.Zero)
                all.Add(nearest);
        }

        return all;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT bounds, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
