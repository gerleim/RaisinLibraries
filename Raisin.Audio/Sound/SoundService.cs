using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Raisin.Audio;

/// <summary>
/// Notification sounds, played through <see cref="MediaPlayer"/>.
/// </summary>
/// <remarks>
/// MediaPlayer rather than <see cref="SoundPlayer"/> because SoundPlayer wraps the Win32
/// PlaySound API and takes uncompressed PCM .wav and nothing else. MediaPlayer goes through
/// Media Foundation, so sounds can ship as small mp3s, and it has a volume control.
/// <para>
/// Players are kept open per file and reused, so decoding is paid for once rather than on
/// every alert. Distinct sounds therefore overlap freely; the same sound retriggered while it
/// is still playing restarts from the top, which is what you want for a UI blip and is in any
/// case rare given <see cref="SoundThrottle"/>.
/// </para>
/// <para>
/// MediaPlayer is a DispatcherObject, so playback is marshalled to the UI dispatcher. With no
/// WPF Application - a unit test, a headless host - file sounds are skipped rather than queued
/// onto a dispatcher that will never run. System sounds still work there.
/// </para>
/// </remarks>
public sealed class SoundService : ISoundService, IDisposable
{
    /// <summary>Long enough that a bell loop becomes a stutter rather than a buzz, short enough not to eat a real second alert.</summary>
    public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromMilliseconds(150);

    /// <summary>Open players held at once. Notification sounds are small and few; this is a ceiling, not a target.</summary>
    private const int MaxCachedPlayers = 8;

    private readonly ISoundSettings _settings;
    private readonly SoundResolver _resolver;
    private readonly SoundThrottle _throttle;
    private readonly SpeechEngine? _speech;
    private readonly SpeechAnnouncer _announcer;
    private readonly Dispatcher? _dispatcher;

    private readonly Dictionary<string, MediaPlayer> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _lru = [];                    // least recently used first
    private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    /// <param name="settings">
    /// Read on every call rather than captured, so a change takes effect on the next sound
    /// without any notification plumbing. See <see cref="ISoundSettings"/>.
    /// </param>
    public SoundService(
        ISoundSettings settings,
        SoundResolver? resolver = null,
        TimeSpan? minInterval = null,
        Dispatcher? dispatcher = null)
    {
        _settings = settings;
        _resolver = resolver ?? new SoundResolver();
        _throttle = new SoundThrottle(minInterval ?? DefaultMinInterval);
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher;

        _speech = new SpeechEngine();
        _announcer = new SpeechAnnouncer(_speech, CurrentSpeechOptions);

        _speech.Progress += (id, offset, length) => SpeechProgress?.Invoke(id, offset, length);
        _speech.Finished += id => SpeechFinished?.Invoke(id);

        EnsureUserSoundsFolder();
    }

    public SoundResolver Resolver => _resolver;

    public bool Enabled
    {
        get => _settings.Enabled;
        set => _settings.Enabled = value;
    }

    public double Volume
    {
        get => Math.Clamp(_settings.Volume, 0, 1);
        set => _settings.Volume = Math.Clamp(value, 0, 1);
    }

    public string Theme
    {
        get => _settings.Theme;
        set => _settings.Theme = value;
    }

    public IReadOnlyList<string> AvailableThemes => _resolver.GetAvailableThemes();

    public string Voice
    {
        get => _settings.Voice;
        set => _settings.Voice = value ?? "";
    }

    public IReadOnlyList<string> AvailableVoices => _speech?.AvailableVoices ?? [];

    /// <summary>Read per utterance, so a voice or rate change is heard on the next one.</summary>
    private SpeechOptions CurrentSpeechOptions()
    {
        return new SpeechOptions(
            _settings.Voice,
            Volume,
            _settings.SpeechRate,
            TimeSpan.FromSeconds(Math.Clamp(_settings.SpeechBatchSeconds, 0.2, 10)));
    }

    public void Play(string? spec, SoundContext? context = null)
    {
        if (_disposed || !Enabled) return;

        SyncTheme();
        var sound = _resolver.Resolve(spec);
        if (sound.IsSilent)
        {
            WarnUnresolvedOnce(spec);
            return;
        }

        // Speech has its own rate limit - a batching window - because dropping an utterance
        // loses the thing it was going to say, where a dropped chime loses nothing.
        if (sound.Kind == SoundKind.Speech)
        {
            _announcer.Announce(new SpeechRequest(
                SpeechPhrase.Parse(sound.Value), context?.Subject, context?.Tokens,
                context?.Count ?? 1));
            return;
        }

        if (!_throttle.TryPlay(sound.Value)) return;

        switch (sound.Kind)
        {
            case SoundKind.System:
                PlaySystemSound(sound.Value);
                break;
            case SoundKind.File:
                Dispatch(() => PlayFile(sound.Value));
                break;
        }
    }

    public bool IsSpeaking => _speech?.IsSpeaking ?? false;

    public event Action<long, int, int>? SpeechProgress;
    public event Action<long>? SpeechFinished;

    public void StopSpeaking() => _speech?.Stop();

    public void CancelPending(string? subject)
    {
        if (_disposed) return;
        _announcer.CancelPending(subject);
    }

    public long Say(string text, string? voice = null, double? rate = null)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text)) return 0;

        var options = CurrentSpeechOptions();
        return _speech?.Speak(text, voice ?? options.Voice, options.Volume, rate ?? options.Rate) ?? 0;
    }

    public void Preload(string? spec)
    {
        if (_disposed) return;

        SyncTheme();
        var sound = _resolver.Resolve(spec);
        if (sound.Kind != SoundKind.File) return;

        Dispatch(() => GetOrCreatePlayer(sound.Value, out _));
    }

    /// <summary>
    /// The theme lives in settings and can change while we are running, so it is picked up per
    /// play rather than captured at construction.
    /// </summary>
    private void SyncTheme()
    {
        var theme = _settings.Theme;
        if (!string.IsNullOrWhiteSpace(theme) && _resolver.Theme != theme)
            _resolver.Theme = theme;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _speech?.Dispose();

        Dispatch(() =>
        {
            foreach (var player in _players.Values)
                ClosePlayer(player);
            _players.Clear();
            _lru.Clear();
        });
    }

    // -- Playback -----------------------------------------------------

    private static void PlaySystemSound(string name)
    {
        var sound = name switch
        {
            "Asterisk" => SystemSounds.Asterisk,
            "Exclamation" => SystemSounds.Exclamation,
            "Hand" => SystemSounds.Hand,
            "Question" => SystemSounds.Question,
            _ => SystemSounds.Beep,
        };

        try { sound.Play(); }
        catch (Exception ex) { Warn($"System sound {name} failed: {ex.Message}"); }
    }

    /// <summary>UI thread only.</summary>
    private void PlayFile(string path)
    {
        var player = GetOrCreatePlayer(path, out bool created);
        if (player is null) return;

        try
        {
            player.Volume = Volume;

            // A player that has only just been opened has no position to rewind, and asking
            // for one before the media is ready throws.
            if (!created) player.Position = TimeSpan.Zero;

            player.Play();
        }
        catch (Exception ex)
        {
            Warn($"Failed to play {path}: {ex.Message}");
            Evict(path);
        }
    }

    /// <summary>UI thread only.</summary>
    private MediaPlayer? GetOrCreatePlayer(string path, out bool created)
    {
        created = false;

        if (_players.TryGetValue(path, out var existing))
        {
            Touch(path);
            return existing;
        }

        try
        {
            var player = new MediaPlayer();

            // Leaving a finished player parked at the end would make the next Play a no-op.
            player.MediaEnded += (_, _) =>
            {
                try { player.Stop(); }
                catch (Exception ex) { Warn($"Stop after end failed for {path}: {ex.Message}"); }
            };

            // A file we cannot decode is not going to start decoding later: drop it rather
            // than hold a player that can only ever fail.
            player.MediaFailed += (_, args) =>
            {
                Warn($"Cannot play {path}: {args.ErrorException?.Message ?? "unknown media error"}");
                Evict(path);
            };

            player.Open(new Uri(path));
            player.Volume = Volume;

            _players[path] = player;
            _lru.Add(path);
            TrimCache();

            created = true;
            return player;
        }
        catch (Exception ex)
        {
            Warn($"Could not open {path}: {ex.Message}");
            return null;
        }
    }

    // -- Player cache -------------------------------------------------

    private void Touch(string path)
    {
        _lru.Remove(path);
        _lru.Add(path);
    }

    private void TrimCache()
    {
        while (_lru.Count > MaxCachedPlayers)
            Evict(_lru[0]);
    }

    private void Evict(string path)
    {
        _lru.Remove(path);
        if (_players.Remove(path, out var player))
            ClosePlayer(player);
    }

    private static void ClosePlayer(MediaPlayer player)
    {
        try
        {
            player.Stop();
            player.Close();
        }
        catch (Exception ex)
        {
            Warn($"Closing player failed: {ex.Message}");
        }
    }

    // -- Plumbing -----------------------------------------------------

    private void Dispatch(Action action)
    {
        if (_dispatcher is null)
        {
            WarnOnce("no-dispatcher", "No WPF dispatcher available; file sounds are disabled for this process.");
            return;
        }

        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }

    private static void EnsureUserSoundsFolder()
    {
        try { Directory.CreateDirectory(SoundResolver.UserSoundsFolder); }
        catch (Exception ex) { Warn($"Could not create sounds folder: {ex.Message}"); }
    }

    private void WarnUnresolvedOnce(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;   // empty means silent, on purpose
        WarnOnce(spec, $"No sound found for [{spec}]. Looked in: {string.Join(", ", _resolver.SearchFolders)}");
    }

    private void WarnOnce(string key, string message)
    {
        lock (_warned)
        {
            if (!_warned.Add(key)) return;
        }
        Warn(message);
    }

    private static void Warn(string message) => AudioLog.Warn($"[Sound] {message}");
}
