namespace Raisin.Audio;

/// <summary>What a spoken alert can talk about. Fills the placeholders in a phrase.</summary>
/// <param name="Subject">
/// What this alert is about, as one name - the thing a batch counts and groups by, and the key
/// <see cref="ISoundService.CancelPending"/> withdraws on. A terminal session, a ticker, a job
/// id: whatever the host thinks of as "one of them". Also available to a phrase as
/// <c>{subject}</c>.
/// </param>
/// <param name="Tokens">
/// The rest of what a phrase may name, by placeholder. <c>{symbol}</c> reads
/// <c>Tokens["symbol"]</c>. A placeholder with nothing behind it is replaced with nothing, so a
/// phrase written for one caller degrades to a shorter sentence rather than reading a brace
/// aloud. Names are matched case-insensitively.
/// </param>
/// <param name="Count">
/// How many things this one alert already stands for. One for the usual case, where each
/// subject speaks for itself and the batching counts the voices; more when the caller has done
/// the counting itself and there is no per-subject alert to collect - a tally of seven open
/// positions at startup is one alert about seven, not seven alerts.
/// </param>
public sealed record SoundContext(
    string? Subject = null,
    IReadOnlyDictionary<string, string>? Tokens = null,
    int Count = 1)
{
    /// <summary>
    /// A context whose subject is also its only token, which is the common case - a symbol that
    /// is both what the batch groups by and what <c>{symbol}</c> reads.
    /// </summary>
    public static SoundContext For(string? subject, string tokenName) =>
        string.IsNullOrWhiteSpace(subject)
            ? new SoundContext()
            : new SoundContext(subject, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [tokenName] = subject,
            });
}

/// <summary>
/// Plays short notification sounds. Every member is safe to call from any thread.
/// </summary>
public interface ISoundService
{
    /// <summary>Master switch. When false, <see cref="Play"/> does nothing.</summary>
    bool Enabled { get; set; }

    /// <summary>Playback volume, 0 to 1. Applies to file sounds only - Windows owns the volume of system sounds.</summary>
    double Volume { get; set; }

    /// <summary>Active sound theme. Every theme carries the same event names, so changing it is safe.</summary>
    string Theme { get; set; }

    /// <summary>Themes found on disk, bundled and user-supplied.</summary>
    IReadOnlyList<string> AvailableThemes { get; }

    /// <summary>Voice for spoken alerts. Blank means whatever Windows defaults to.</summary>
    string Voice { get; set; }

    /// <summary>
    /// Play a sound spec (see <see cref="SoundResolver"/>). Empty, unknown or throttled specs
    /// are silent. A spoken spec is batched rather than played, and uses the context to fill
    /// its placeholders.
    /// </summary>
    void Play(string? spec, SoundContext? context = null);

    /// <summary>
    /// Withdraws anything this subject has queued to be spoken but not yet said.
    /// </summary>
    /// <remarks>
    /// For the case where an alert has been overtaken by events inside its own batching
    /// window - the thing was announced as needing you, and it stopped needing you before the
    /// sentence was composed. Chimes have no such window and are already out; this is only
    /// about the words.
    /// </remarks>
    void CancelPending(string? subject);

    /// <summary>Speech voices installed on this machine. Empty when speech is unavailable.</summary>
    IReadOnlyList<string> AvailableVoices { get; }

    /// <summary>
    /// Say something now, in the given voice, skipping the batching window and the master
    /// switch. This is for auditioning a voice in the options: a click asking to hear something
    /// should be answered, and answered immediately rather than a second and a half later.
    /// </summary>
    /// <param name="rate">
    /// Overrides the configured speaking rate for this utterance. Used to run a reading faster
    /// while a key is held - the rate of speech in progress cannot be changed, so going faster
    /// means saying the rest of it again at a different rate.
    /// </param>
    /// <returns>
    /// The id of this utterance. Speaking cancels what came before, so a caller following one
    /// reading needs it to tell its own progress and its own ending from the last one's.
    /// </returns>
    long Say(string text, string? voice = null, double? rate = null);

    /// <summary>Stop whatever is being spoken. Pressing the read key again should silence it.</summary>
    void StopSpeaking();

    /// <summary>True while an utterance is in progress.</summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Where speech has reached in the text it was given: the utterance's id, then the offset
    /// and length of the current word. Fires off the UI thread.
    /// </summary>
    event Action<long, int, int>? SpeechProgress;

    /// <summary>Raised when an utterance stops, finished or cancelled, with its id.</summary>
    event Action<long>? SpeechFinished;

    /// <summary>Open a sound ahead of time so the first play is not the one that pays for decoding.</summary>
    void Preload(string? spec);
}
