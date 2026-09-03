using Raisin.EventSystem;

namespace Raisin.WPF.Base;

/// <summary>
/// Announces that a window now occupies a different display, or that its display's refresh
/// rate changed.
/// </summary>
/// <remarks>
/// Announcement, not transport. A control that paces itself to the display holds its window's
/// <see cref="WindowDisplayInfo"/> and reads the value from it; this exists for the open-ended
/// set of observers that merely want to know - diagnostics, logging, anything measuring frame
/// rates. Routing the value itself through the bus would need a window identity on every
/// message and a filter on every subscriber, would leave a control created later with no
/// current value, and would put a SynchronizationContext hop between a display change and a
/// read inside a render loop.
///
/// <see cref="Hwnd"/> identifies the window, so a subscriber interested in one of several
/// floating windows can filter on it.
/// </remarks>
public class DisplayChangedArgs(IntPtr hwnd, string devices, int refreshRate) : EventSystemEventArgs
{
    /// <summary>Handle of the window whose display changed.</summary>
    public IntPtr Hwnd { get; } = hwnd;

    /// <summary>Device names of every display the window now touches.</summary>
    public string Devices { get; } = devices;

    /// <summary>Refresh rate in hertz of the fastest of them.</summary>
    public int RefreshRate { get; } = refreshRate;
}
