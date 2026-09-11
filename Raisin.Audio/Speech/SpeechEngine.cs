using System.Speech.Synthesis;

namespace Raisin.Audio;

/// <summary>
/// Windows speech, through System.Speech (SAPI 5).
/// </summary>
/// <remarks>
/// One synthesiser is kept for the life of the app: constructing one costs enough to be heard
/// as a delay before the first word. SpeakAsync is not a DispatcherObject and is safe from any
/// thread, so unlike MediaPlayer this needs no marshalling.
/// <para>
/// SAPI reaches the classic voices - David, Zira, Mark. The newer Windows 11 natural voices are
/// WinRT-only and would need the project's target framework moved to a Windows SDK version.
/// </para>
/// </remarks>
public sealed class SpeechEngine : ISpeechEngine, IDisposable
{
    private readonly Lazy<SpeechSynthesizer?> _synthesizer;
    /// <summary>Utterances in progress, each against the id it was given.</summary>
    private readonly Dictionary<Prompt, long> _outstanding = [];

    private long _sequence;

    /// <summary>
    /// Utterances handed over but not yet queued. Counted so that cancelling one in order to
    /// start another does not read as speech having stopped: for the moment between the cancel
    /// and the new prompt being registered, nothing is outstanding, and a listener that took
    /// that as the end would tear down a reading that is about to carry on.
    /// </summary>
    private int _starting;
    private readonly object _speakGate = new();
    private volatile bool _disposed;

    /// <summary>
    /// The synthesiser is built on first use and wired for SpeakCompleted as it is built, so
    /// <see cref="IsSpeaking"/> can be trusted without forcing construction at startup.
    /// </summary>
    public SpeechEngine()
    {
        _synthesizer = new Lazy<SpeechSynthesizer?>(() =>
        {
            var synthesizer = Create();
            if (synthesizer is not null)
            {
                synthesizer.SpeakCompleted += (_, args) =>
                {
                    long id;
                    lock (_outstanding)
                    {
                        _outstanding.Remove(args.Prompt, out id);
                    }

                    // Always reported, cancellation included. Which utterance it was is what
                    // tells a listener whether this was the end of what it was following or the
                    // one it just replaced.
                    Finished?.Invoke(id);
                };

                // CharacterPosition indexes the string we handed over, unchanged - which is what
                // lets a caller map it back to where the words came from.
                synthesizer.SpeakProgress += (_, args) =>
                {
                    long id;
                    lock (_outstanding)
                    {
                        if (!_outstanding.TryGetValue(args.Prompt, out id)) return;
                    }

                    Progress?.Invoke(id, args.CharacterPosition, args.CharacterCount);
                };
            }
            return synthesizer;
        });
    }

    /// <inheritdoc/>
    public event Action<long, int, int>? Progress;

    /// <inheritdoc/>
    public event Action<long>? Finished;

    /// <summary>
    /// True from the moment an utterance is handed over. One still on its way counts: the batched
    /// alerts wait for silence before speaking, and a gap of a few milliseconds while a reading
    /// restarts is not silence - taking it for silence is how an alert ends up cutting a reading off.
    /// </summary>
    public bool IsSpeaking
    {
        get { lock (_outstanding) return _outstanding.Count > 0 || _starting > 0; }
    }

    public IReadOnlyList<string> AvailableVoices
    {
        get
        {
            try
            {
                return _synthesizer.Value?.GetInstalledVoices()
                    .Where(v => v.Enabled)
                    .Select(v => v.VoiceInfo.Name)
                    .ToList() ?? [];
            }
            catch (Exception ex)
            {
                Warn($"Could not list voices: {ex.Message}");
                return [];
            }
        }
    }

    /// <summary>
    /// Hands the words to the synthesiser without ever blocking the caller.
    /// </summary>
    /// <remarks>
    /// Every call here happens off the calling thread on purpose. SelectVoice and the Volume and
    /// Rate setters block while an utterance is in progress, so auditioning a voice from the
    /// options pane during a long narration froze the window for as long as the reading lasted.
    /// The UI thread must never touch this object.
    /// <para>
    /// Speaking cancels whatever is already being said rather than queueing behind it. A new
    /// request is always the one you want to hear; the alternative is waiting out a screen of
    /// output to find out what a voice sounds like.
    /// </para>
    /// </remarks>
    public long Speak(string text, string? voice, double volume, double rate)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text)) return 0;

        // Numbered here rather than off the thread, so the caller has the id in hand before the
        // utterance it belongs to has started - the events can otherwise beat it back.
        long id = Interlocked.Increment(ref _sequence);

        Task.Run(() => SpeakOffThread(id, text, voice, volume, rate));
        return id;
    }

    private void SpeakOffThread(long id, string text, string? voice, double volume, double rate)
    {
        lock (_speakGate)
        {
            var synthesizer = _synthesizer.Value;
            if (synthesizer is null || _disposed) return;

            // Claimed before the cancel below, so the completion it provokes arrives while this
            // one is on its way and is not mistaken for silence.
            lock (_outstanding) _starting++;

            try
            {
                synthesizer.SpeakAsyncCancelAll();

                if (!string.IsNullOrWhiteSpace(voice)) SelectVoice(synthesizer, voice);
                synthesizer.Volume = (int)Math.Round(Math.Clamp(volume, 0, 1) * 100);
                synthesizer.Rate = (int)Math.Round(Math.Clamp(rate, -10, 10));

                // The prompt is built first so it can be recorded before it can complete -
                // a short phrase can finish before SpeakAsync has even returned.
                var prompt = new Prompt(text);
                lock (_outstanding) _outstanding[prompt] = id;

                synthesizer.SpeakAsync(prompt);
            }
            catch (Exception ex)
            {
                Warn($"Could not speak: {ex.Message}");
            }
            finally
            {
                lock (_outstanding) _starting--;
            }
        }
    }

    public void Stop()
    {
        if (_disposed || !_synthesizer.IsValueCreated) return;

        // Off-thread for the same reason as Speak: cancelling takes the synthesiser's lock.
        Task.Run(() =>
        {
            lock (_speakGate)
            {
                try { _synthesizer.Value?.SpeakAsyncCancelAll(); }
                catch (Exception ex) { Warn($"Could not stop speaking: {ex.Message}"); }
            }
        });
    }

    private static void SelectVoice(SpeechSynthesizer synthesizer, string voice)
    {
        try { synthesizer.SelectVoice(voice); }
        catch (ArgumentException)
        {
            // A voice named in settings that this machine no longer has: keep the default one
            // rather than losing the announcement.
            Warn($"Voice '{voice}' is not installed; using the default.");
        }
    }

    private static SpeechSynthesizer? Create()
    {
        try
        {
            var synthesizer = new SpeechSynthesizer();
            if (synthesizer.GetInstalledVoices().All(v => !v.Enabled))
            {
                Warn("No speech voices are installed; spoken alerts will be silent.");
                synthesizer.Dispose();
                return null;
            }
            return synthesizer;
        }
        catch (Exception ex)
        {
            Warn($"Speech is unavailable: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_synthesizer.IsValueCreated) return;

        // Under the same lock as Speak, so shutdown cannot dispose the synthesiser out from
        // under an utterance being queued on the background thread.
        lock (_speakGate)
        {
            try
            {
                _synthesizer.Value?.SpeakAsyncCancelAll();
                _synthesizer.Value?.Dispose();
            }
            catch (Exception ex) { Warn($"Closing the synthesiser failed: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Installed voices for the options picker, which is a static list and so cannot ask the
    /// running service. Cached: enumerating them constructs a synthesiser. The blank first entry
    /// means "whatever Windows defaults to".
    /// </summary>
    public static string[] InstalledVoices => _installed.Value;

    private static readonly Lazy<string[]> _installed = new(() =>
    {
        try
        {
            using var synthesizer = new SpeechSynthesizer();
            return ["", .. synthesizer.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)];
        }
        catch (Exception)
        {
            return [""];        // a picker with only the default beats a settings page that will not open
        }
    });

    private static void Warn(string message) => AudioLog.Warn($"[Speech] {message}");

}
