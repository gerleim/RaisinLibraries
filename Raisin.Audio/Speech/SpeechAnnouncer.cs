namespace Raisin.Audio;

/// <summary>What actually says the words. Kept behind an interface so the batching is testable.</summary>
public interface ISpeechEngine
{
    /// <summary>True while an utterance is still being spoken.</summary>
    bool IsSpeaking { get; }

    /// <summary>Voices installed on this machine, most preferred first. Empty if speech is unavailable.</summary>
    IReadOnlyList<string> AvailableVoices { get; }

    /// <summary>
    /// Speak, without blocking the caller. Returns the id of this utterance, which the events
    /// below carry back: speaking cancels whatever came before, and a listener following one
    /// utterance has to be able to tell its own progress from the tail of the last one.
    /// </summary>
    long Speak(string text, string? voice, double volume, double rate);

    /// <summary>Stop mid-sentence. Narration is long enough that stopping it matters.</summary>
    void Stop();

    /// <summary>
    /// Raised as each word is reached, with the utterance's id and the offset and length of the
    /// word in the text that was handed over. Fires on the synthesiser's own thread.
    /// </summary>
    event Action<long, int, int>? Progress;

    /// <summary>
    /// Raised when an utterance ends, whether it finished or was cancelled, with its id.
    /// </summary>
    event Action<long>? Finished;
}

/// <summary>
/// Collects spoken alerts over a short window and says them as one sentence.
/// </summary>
/// <remarks>
/// Chimes are rate limited: a second one inside 150ms is dropped because you already heard the
/// first. Speech cannot work that way - a dropped utterance loses the thing it was going to
/// tell you, and two overlapping ones are unintelligible. So instead of discarding, this waits
/// briefly, folds everything that arrived into one sentence, and says that.
/// <para>
/// The window spans every event rather than each kind separately, or two different alerts would
/// still collide. Anything arriving while an utterance is in progress joins the next batch
/// rather than interrupting.
/// </para>
/// </remarks>
public sealed class SpeechAnnouncer
{
    private readonly ISpeechEngine _engine;
    private readonly Func<SpeechOptions> _options;
    private readonly Action<TimeSpan, Action> _schedule;

    private readonly List<SpeechRequest> _pending = [];
    private readonly object _gate = new();
    private bool _flushScheduled;

    /// <param name="schedule">
    /// Runs the callback after the delay. Injected so tests drive the window directly instead of
    /// waiting on a real timer.
    /// </param>
    public SpeechAnnouncer(
        ISpeechEngine engine,
        Func<SpeechOptions> options,
        Action<TimeSpan, Action>? schedule = null)
    {
        _engine = engine;
        _options = options;
        _schedule = schedule ?? DefaultSchedule;
    }

    public void Announce(SpeechRequest request)
    {
        if (request.Phrase.IsEmpty) return;

        lock (_gate)
        {
            _pending.Add(request);
            if (_flushScheduled) return;        // a window is already open; this joins it
            _flushScheduled = true;
        }

        _schedule(_options().BatchWindow, Flush);
    }

    /// <summary>
    /// Takes back whatever a subject has waiting to be said, if the window has not closed yet.
    /// </summary>
    /// <remarks>
    /// The batching window is a grace period nothing else has: for the second and a half
    /// between an alert being raised and being spoken, it can still be withdrawn as though it
    /// had never happened. That is exactly long enough to cover answering a prompt the moment
    /// it appears, which is the case where the alert is least wanted and most annoying.
    /// <para>
    /// Matched on the subject, the same key the batch counts by - so two subjects sharing a
    /// name withdraw for each other, which is the failure the count already has and neither is
    /// worth an id to fix.
    /// </para>
    /// </remarks>
    public void CancelPending(string? subject)
    {
        if (string.IsNullOrEmpty(subject)) return;

        lock (_gate)
            _pending.RemoveAll(r => string.Equals(r.Subject, subject, StringComparison.Ordinal));
    }

    private void Flush()
    {
        List<SpeechRequest> batch;
        lock (_gate)
        {
            // Still talking: leave everything pending and look again after another window,
            // so the next sentence starts cleanly rather than over the top of this one.
            if (_engine.IsSpeaking)
            {
                _schedule(_options().BatchWindow, Flush);
                return;
            }

            batch = [.. _pending];
            _pending.Clear();
            _flushScheduled = false;
        }

        if (batch.Count == 0) return;

        var text = SpeechBatch.Compose(batch);
        if (text.Length == 0) return;

        var options = _options();
        _engine.Speak(text, options.Voice, options.Volume, options.Rate);
    }

    private static void DefaultSchedule(TimeSpan delay, Action action) =>
        _ = Task.Delay(delay).ContinueWith(_ => action(), TaskScheduler.Default);
}

/// <summary>How the speech should sound, read fresh each time so settings changes take effect.</summary>
public sealed record SpeechOptions(string? Voice, double Volume, double Rate, TimeSpan BatchWindow);
