using System.Drawing;
using System.Runtime.InteropServices;

namespace Raisin.WPF.Automation;

/// <summary>One attached display, as the desktop currently has it configured.</summary>
public sealed record DisplayInfo(string DeviceName, Rectangle Bounds, int RefreshHz, bool IsPrimary)
{
    /// <summary>The frame budget this panel imposes, in milliseconds.</summary>
    public double FramePeriodMs => RefreshHz > 0 ? 1000.0 / RefreshHz : 0;

    public override string ToString() =>
        $"{DeviceName}  {Bounds.Width}x{Bounds.Height} @ {RefreshHz} Hz{(IsPrimary ? "  PRIMARY" : "")}";
}

/// <summary>
/// What displays are attached, and — the part nothing else here reports — how fast each one refreshes.
/// </summary>
/// <remarks>
/// A capture that does not record the refresh rate it was taken at cannot be compared with another
/// one, because the frame budget is the refresh period and it varies by a factor of nearly five
/// across ordinary desktop panels. <see cref="TargetWindow"/> explains why that matters to a result;
/// this is where the number comes from.
///
/// <c>System.Windows.Forms.Screen</c> is the usual way to enumerate displays and is the easier API,
/// but it exposes bounds and working area only. The rate needs <c>EnumDisplaySettings</c>, whose
/// <c>DEVMODE</c> has to be laid out exactly right — a wrong field offset does not fail, it returns
/// a plausible number from the wrong part of the struct.
/// </remarks>
public static class Displays
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DISPLAY_DEVICE info, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint AttachedToDesktop = 0x1;
    private const uint PrimaryDevice = 0x4;

    /// <summary>Every display attached to the desktop, in adapter order.</summary>
    /// <remarks>
    /// Adapters that are present but not attached are skipped: they have no position and no mode, so
    /// including them would put entries in the list that cannot be measured on.
    /// </remarks>
    public static IReadOnlyList<DisplayInfo> All()
    {
        var found = new List<DisplayInfo>();

        for (uint i = 0; ; i++)
        {
            var dd = new DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
            if (!EnumDisplayDevices(null, i, ref dd, 0)) break;
            if ((dd.StateFlags & AttachedToDesktop) == 0) continue;

            var dm = new DEVMODE();
            dm.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
            if (!EnumDisplaySettings(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref dm)) continue;

            found.Add(new DisplayInfo(
                dd.DeviceName,
                new Rectangle(dm.dmPositionX, dm.dmPositionY, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight),
                (int)dm.dmDisplayFrequency,
                (dd.StateFlags & PrimaryDevice) != 0));
        }

        return found;
    }

    /// <summary>The refresh rate of a named display, or 0 if it is not attached.</summary>
    /// <param name="deviceName">A device name as <see cref="All"/> reports it, such as <c>\.\DISPLAY1</c>.</param>
    public static int RefreshHz(string deviceName)
    {
        var dm = new DEVMODE();
        dm.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
        return EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm) ? (int)dm.dmDisplayFrequency : 0;
    }

    /// <summary>The display a window is mostly on, by largest overlap.</summary>
    /// <remarks>
    /// Largest overlap rather than the top-left corner, so a window straddling a boundary is
    /// attributed to the panel actually presenting most of it — which is the one whose refresh
    /// rate the frames were paced by.
    /// </remarks>
    public static DisplayInfo? For(Rectangle window)
    {
        DisplayInfo? best = null;
        var bestArea = 0L;

        foreach (var d in All())
        {
            var overlap = Rectangle.Intersect(d.Bounds, window);
            long area = (long)overlap.Width * overlap.Height;
            if (area > bestArea) { bestArea = area; best = d; }
        }

        return best ?? All().FirstOrDefault(d => d.IsPrimary);
    }
}
