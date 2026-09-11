namespace Raisin.Audio;

/// <summary>
/// The playback settings, as the host stores them.
/// </summary>
/// <remarks>
/// Deliberately a live view rather than a snapshot: <see cref="SoundService"/> reads these on
/// every call and caches nothing, which is what makes a volume drag audible on the very next
/// chime without a change notification anywhere in the system. An implementation must therefore
/// read through to whatever the app actually persists, not copy it at construction.
/// <para>
/// The four settable members are settable because the options pane applies them as they are
/// picked - you cannot judge a volume you will only hear after pressing Apply. The two read-only
/// ones have no such need: they are read fresh per utterance anyway.
/// </para>
/// </remarks>
public interface ISoundSettings
{
    /// <summary>Master switch. When false, <see cref="ISoundService.Play"/> does nothing.</summary>
    bool Enabled { get; set; }

    /// <summary>Playback volume, 0 to 1. Clamped by the service, so an out-of-range value is harmless.</summary>
    double Volume { get; set; }

    /// <summary>Active sound theme. Blank leaves the resolver on whatever it already had.</summary>
    string Theme { get; set; }

    /// <summary>Voice for spoken alerts. Blank means whatever Windows defaults to.</summary>
    string Voice { get; set; }

    /// <summary>Speaking rate: -10 slowest, 0 normal, 10 fastest.</summary>
    double SpeechRate { get; }

    /// <summary>How long spoken alerts are collected before being said as one sentence.</summary>
    double SpeechBatchSeconds { get; }
}
