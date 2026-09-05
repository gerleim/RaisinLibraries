using System.Drawing;
using System.Runtime.InteropServices;

namespace Raisin.WPF.Automation;

/// <summary>
/// A window being driven from outside: where it is, and where to put it before a run.
/// </summary>
/// <remarks>
/// Placement belongs in a measurement harness rather than being left to wherever the app last
/// opened, because both of these change the numbers:
///
/// <list type="bullet">
/// <item>
/// <b>Size.</b> A taller window holds more content, so more of it has to be produced per frame.
/// Whatever the per-frame work scales with — lines, rows, plotted points — window height is its
/// multiplier.
/// </item>
/// <item>
/// <b>Which display.</b> The frame budget is the refresh period: 3.6 ms on a 280 Hz panel against
/// 16.7 ms on a 60 Hz one, a factor of nearly five. A result that holds on one and not the other is
/// telling you the work is near the limit rather than that it is broken.
/// </item>
/// </list>
///
/// Vary one at a time. Moving to a slower panel that is also smaller changes both, and then neither
/// number can be attributed.
/// </remarks>
public sealed class TargetWindow(IntPtr handle)
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    private const int SW_RESTORE = 9;
    private const int SW_MAXIMIZE = 3;

    public IntPtr Handle { get; } = handle;

    /// <summary>The window's screen rectangle, or empty if it has gone.</summary>
    public Rectangle Bounds =>
        GetWindowRect(Handle, out var r)
            ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)
            : Rectangle.Empty;

    /// <summary>Brings it to the front, or throws.</summary>
    public void Focus(TimeSpan timeout, string what = "the target window")
        => ForegroundWindow.EnsureOrThrow(Handle, timeout, what);

    /// <summary>Puts the window at an exact rectangle.</summary>
    /// <remarks>
    /// Restored first: a maximised window ignores a move, and does so without complaining, so a run
    /// that meant to resize would quietly measure the old size.
    /// </remarks>
    public void PlaceAt(Rectangle bounds)
    {
        ShowWindow(Handle, SW_RESTORE);
        MoveWindow(Handle, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
    }

    /// <summary>Fills a screen's working area — the whole panel less the taskbar.</summary>
    /// <remarks>
    /// The working area rather than the full bounds, because a window placed over the taskbar is not
    /// a configuration anyone runs the app in.
    /// </remarks>
    public void FillWorkingArea(Rectangle workingArea) => PlaceAt(workingArea);

    /// <summary>Maximises on whichever display the window is currently on.</summary>
    public void Maximise() => ShowWindow(Handle, SW_MAXIMIZE);

    /// <summary>
    /// A point inside the window, given as fractions of its width and height.
    /// </summary>
    /// <remarks>
    /// Aim with this rather than at the centre. Docked panes take fixed widths, so at small window
    /// sizes the geometric centre can land on a splitter, a scrollbar or a neighbouring pane — and a
    /// gesture delivered to the wrong control still produces input, still completes, and measures
    /// something else entirely.
    /// </remarks>
    public Point PointAt(double fractionX, double fractionY)
    {
        var b = Bounds;
        return new Point(
            b.X + (int)(b.Width * fractionX),
            b.Y + (int)(b.Height * fractionY));
    }
}
