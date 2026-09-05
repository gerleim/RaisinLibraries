using System.Runtime.InteropServices;

namespace Raisin.WPF.Automation;

/// <summary>
/// Brings a window to the front so that synthetic input reaches it.
/// </summary>
/// <remarks>
/// This exists because <c>SetForegroundWindow</c> is one of the more treacherous calls in user32.
/// Windows refuses it unless the calling process is already in the foreground, and it reports the
/// refusal by returning <c>false</c> rather than by throwing — so a caller that does not check
/// carries on and sends its whole gesture to whatever window happened to be in front.
///
/// That failure is silent in the worst way: the run completes, the log fills, and the numbers
/// describe an application nobody was driving. Both apps in this family have hit it.
/// </remarks>
public static class ForegroundWindow
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    /// <summary>
    /// Brings <paramref name="hwnd"/> to the front, and says whether it got there.
    /// </summary>
    /// <returns>
    /// True if the window is foreground when this returns. A caller that is about to send input
    /// should treat false as fatal rather than carrying on.
    /// </returns>
    /// <remarks>
    /// Attaching to the foreground thread's input queue lifts the restriction for as long as the
    /// attachment lasts, which is the standard way through.
    ///
    /// The result is verified against <c>GetForegroundWindow</c> rather than trusted, because the
    /// return value is not the whole story — the call can report success and the window still not be
    /// foreground.
    /// </remarks>
    public static bool Ensure(IntPtr hwnd, TimeSpan timeout)
    {
        if (hwnd == IntPtr.Zero) return false;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (GetForegroundWindow() == hwnd) return true;

            if (!SetForegroundWindow(hwnd))
            {
                var us = GetCurrentThreadId();
                var them = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);

                if (them != 0 && them != us && AttachThreadInput(us, them, true))
                {
                    SetForegroundWindow(hwnd);
                    AttachThreadInput(us, them, false);
                }
            }

            Thread.Sleep(120);
        }

        return GetForegroundWindow() == hwnd;
    }

    /// <summary>Brings a window to the front, or throws saying what it could not do.</summary>
    /// <remarks>
    /// The throwing form is usually the one you want in a measurement run: a capture taken against
    /// the wrong window is worse than no capture, because it looks like data.
    /// </remarks>
    public static void EnsureOrThrow(IntPtr hwnd, TimeSpan timeout, string what = "the target window")
    {
        if (!Ensure(hwnd, timeout))
            throw new InvalidOperationException(
                $"Could not bring {what} to the foreground within {timeout.TotalSeconds:F1}s. " +
                "Refusing to send input, which would otherwise go to whatever is in front.");
    }
}
