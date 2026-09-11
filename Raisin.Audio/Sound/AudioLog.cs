namespace Raisin.Audio;

/// <summary>
/// Where the audio stack reports trouble: a missing sound file, a voice that is no longer
/// installed, a codec that will not decode.
/// </summary>
/// <remarks>
/// A settable hook rather than an injected logger because the two places that report - the
/// player and the synthesiser - do so from static helpers reached by static factory methods,
/// and threading a delegate to all of them would cost more than it buys. A library should not
/// take a dependency on an event bus just to say a file is missing, so the host supplies
/// whatever it logs through, once, at startup.
/// <para>
/// Nothing here is fatal. Left unset, warnings are simply dropped: an app that never wires this
/// up plays its sounds and stays silent about the ones it could not find.
/// </para>
/// </remarks>
public static class AudioLog
{
    /// <summary>Called with an already-formatted warning. May fire from any thread.</summary>
    public static Action<string>? OnWarning { get; set; }

    /// <summary>
    /// Reports a warning, swallowing anything the handler throws - a logger that fails is not a
    /// reason for a chime to fail.
    /// </summary>
    internal static void Warn(string message)
    {
        var handler = OnWarning;
        if (handler is null) return;

        try { handler(message); }
        catch (Exception) { }
    }
}
