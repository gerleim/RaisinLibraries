using System.Drawing;
using System.Runtime.InteropServices;

namespace Raisin.WPF.Automation;

/// <summary>
/// Real mouse input, sent through <c>SendInput</c> so it arrives the way a person's does.
/// </summary>
/// <remarks>
/// The alternative is always tempting and always wrong for measurement: driving the gesture from
/// inside the target process, or posting messages to a window handle. Neither goes through the
/// input queue, and where the input queue sits behind the render work is frequently the very thing
/// being measured — so measuring it that way assumes the answer.
///
/// Two things this API exists to stop callers getting wrong, both learned by getting them wrong:
///
/// <list type="bullet">
/// <item>
/// WPF routes a wheel message to whatever sits under the <b>physical</b> cursor and ignores the
/// coordinates in a posted message. The cursor has to actually move, which is why
/// <see cref="WheelAt"/> takes a point rather than a window.
/// </item>
/// <item>
/// A drag is a stream of moves. Jumping straight from start to finish delivers one mouse-move
/// message, and the continuous repaint that a real drag causes — usually the load under test — never
/// happens.
/// </item>
/// </list>
///
/// Synthetic <b>keyboard</b> input is deliberately absent. It was tried against a terminal control
/// and never arrived, by either <c>KEYEVENTF_UNICODE</c> or real virtual keys. Design around needing
/// to type rather than rediscovering that.
/// </remarks>
public static class SyntheticInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    /// <summary>One wheel detent, as Windows counts them.</summary>
    private const int WheelDelta = 120;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point p);

    private static void Send(uint flags, uint data = 0)
    {
        var input = new INPUT[1];
        input[0].type = INPUT_MOUSE;
        input[0].mi.dwFlags = flags;
        input[0].mi.mouseData = data;
        SendInput(1, input, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Where the cursor is now.</summary>
    public static Point CursorPosition
    {
        get { GetCursorPos(out var p); return p; }
        set { SetCursorPos(value.X, value.Y); }
    }

    /// <summary>
    /// Turns the wheel over a point, moving the cursor there first.
    /// </summary>
    /// <param name="notches">
    /// Detents. Negative scrolls down — the direction a reader moves forward through a document.
    /// </param>
    /// <remarks>
    /// The move is not optional. WPF delivers the message to the window under the physical pointer,
    /// so wheeling without moving the cursor scrolls whatever it happens to be resting on.
    /// </remarks>
    public static void WheelAt(Point at, int notches, int gapMs = 0)
    {
        CursorPosition = at;
        Thread.Sleep(60);   // let the enter/hover settle before the first notch

        var step = Math.Sign(notches);
        for (var i = 0; i < Math.Abs(notches); i++)
        {
            Send(MOUSEEVENTF_WHEEL, unchecked((uint)(step * WheelDelta)));
            if (gapMs > 0) Thread.Sleep(gapMs);
        }
    }

    /// <summary>
    /// Presses at <paramref name="from"/>, moves to <paramref name="to"/> in steps, and releases.
    /// </summary>
    /// <remarks>
    /// The button is released in a finally: a run that throws mid-drag and leaves the button down
    /// hands the machine back with the mouse stuck, which is a worse outcome than the failed test.
    /// </remarks>
    public static void Drag(Point from, Point to, int steps = 40, int stepDelayMs = 12)
    {
        if (steps < 1) steps = 1;

        CursorPosition = from;
        Thread.Sleep(60);
        Send(MOUSEEVENTF_LEFTDOWN);

        try
        {
            for (var i = 1; i <= steps; i++)
            {
                CursorPosition = new Point(
                    from.X + (to.X - from.X) * i / steps,
                    from.Y + (to.Y - from.Y) * i / steps);
                Thread.Sleep(stepDelayMs);
            }
        }
        finally
        {
            Send(MOUSEEVENTF_LEFTUP);
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> and puts the cursor back where it was.
    /// </summary>
    /// <remarks>
    /// A measurement run borrows the pointer from whoever is at the machine. Returning it is the
    /// difference between an unattended run and one that quietly relocates someone's cursor.
    /// </remarks>
    public static void PreservingCursor(Action body)
    {
        var saved = CursorPosition;
        try { body(); }
        finally { CursorPosition = saved; }
    }
}
