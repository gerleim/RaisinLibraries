using System.Drawing;
using System.Runtime.InteropServices;

namespace Raisin.WPF.Automation;

/// <summary>
/// Watches an automated run for the two things that invalidate it: someone taking the machine
/// back, and someone wanting it back.
/// </summary>
/// <remarks>
/// A synthetic gesture borrows the pointer and the foreground window from whoever is sitting at the
/// machine. Two failures follow from that, and neither announces itself:
///
/// <list type="bullet">
/// <item>
/// <b>Interference.</b> A click into another window, a nudge of the mouse, a notification stealing
/// focus - and from then on the gesture is landing somewhere else, or landing in the right place on
/// top of a person's own input. The run still completes. The log still fills. The numbers describe
/// a mixture of the harness and a human, and nothing in the output says so.
/// </item>
/// <item>
/// <b>No way out.</b> A run that has taken the cursor and cannot be told to stop leaves the person
/// at the machine waiting for it - which is unpleasant enough that they will take the mouse back,
/// producing the first failure.
/// </item>
/// </list>
///
/// So the two belong together. <see cref="ThrowIfCancelled"/> gives the machine back on demand;
/// <see cref="Check"/> notices when it was taken without asking. Cancelling is not interference: it
/// is checked first, and a cancelled run is abandoned rather than reported as contaminated.
/// </remarks>
public sealed class RunGuard : IDisposable
{
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    private const int VK_ESCAPE = 0x1B;

    /// <summary>True while Escape is held, wherever the focus happens to be.</summary>
    /// <remarks>
    /// Asked of the keyboard rather than of a window, because the harness owns the foreground and a
    /// key pressed while it does would be delivered to the application under test and swallowed
    /// there. The cancel key has to be one that works without anything being focused.
    /// </remarks>
    public static bool CancelKeyDown => (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;

    private readonly TargetWindow _target;
    private readonly Rectangle _placedAt;
    private readonly List<string> _violations = [];

    /// <summary>What was seen to interfere, in the order it was noticed.</summary>
    public IReadOnlyList<string> Violations => _violations;

    /// <summary>True while nothing has interfered, and the run is still worth keeping.</summary>
    public bool Clean => _violations.Count == 0;

    /// <summary>Starts watching, and arms cancellation for every gesture sent while this lives.</summary>
    public RunGuard(TargetWindow target)
    {
        _target = target;
        _placedAt = target.Bounds;
        SyntheticInput.ShouldAbort = () => CancelKeyDown;
    }

    /// <summary>Stops the run if Escape is being held.</summary>
    /// <remarks>
    /// Throwing rather than returning a flag, so a caller cannot forget to act on it, and so that
    /// the button release in <see cref="SyntheticInput.Drag"/> and the cursor restore in
    /// <see cref="SyntheticInput.PreservingCursor"/> both run on the way out. A run cancelled
    /// mid-drag must not hand the machine back with the mouse button held down.
    /// </remarks>
    public void ThrowIfCancelled()
    {
        if (CancelKeyDown)
            throw new OperationCanceledException("Escape held - run cancelled, the machine is yours.");
    }

    /// <summary>
    /// Looks for interference, and records it against <paramref name="where"/> in the run.
    /// </summary>
    /// <remarks>
    /// Three symptoms, because the same act shows up differently depending on what was touched: the
    /// foreground moving means input is now going elsewhere; the window moving means a placed run is
    /// no longer at the size and display it was placed at; the pointer sitting somewhere other than
    /// where the harness last put it means a hand is on the mouse.
    ///
    /// Each kind is recorded once. Someone who has taken the mouse back trips the same check on
    /// every subsequent gesture, and forty identical lines say no more than one does.
    /// </remarks>
    public void Check(string where)
    {
        ThrowIfCancelled();

        if (GetForegroundWindow() != _target.Handle)
            Record("another window came to the front", where);

        var now = _target.Bounds;
        if (!now.IsEmpty && now != _placedAt)
            Record($"the window moved or was resized - placed at {_placedAt}, now {now}", where);

        if (SyntheticInput.LastCommanded is { } want)
        {
            var actual = SyntheticInput.CursorPosition;
            // A couple of pixels of slack. The pointer goes where it is put, but this should report
            // a hand on the mouse, not a rounding difference.
            if (Math.Abs(actual.X - want.X) > 2 || Math.Abs(actual.Y - want.Y) > 2)
                Record($"the pointer was moved - left at {want}, found at {actual}", where);
        }
    }

    private void Record(string what, string where)
    {
        if (_violations.Any(v => v.StartsWith(what, StringComparison.Ordinal))) return;
        _violations.Add($"{what} (during {where})");
    }

    /// <summary>One line saying whether the run was clean, for the record kept beside a capture.</summary>
    public string Summary() => Clean ? "none detected" : string.Join("; ", _violations);

    public void Dispose() => SyntheticInput.ShouldAbort = null;
}
