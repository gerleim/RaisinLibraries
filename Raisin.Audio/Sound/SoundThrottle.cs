namespace Raisin.Audio;

/// <summary>
/// Rate limit, per sound.
/// </summary>
/// <remarks>
/// A program in a loop can emit hundreds of bells a second. Without a gate every one of
/// them restarts a player from the top, and the result is a buzz rather than a bell.
/// Keyed by resolved sound so a flood of one sound does not swallow a different sound
/// that happens to fire during it.
/// </remarks>
public sealed class SoundThrottle
{
    private readonly TimeProvider _time;
    private readonly Dictionary<string, DateTimeOffset> _lastPlayed = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public SoundThrottle(TimeSpan minInterval, TimeProvider? time = null)
    {
        MinInterval = minInterval;
        _time = time ?? TimeProvider.System;
    }

    public TimeSpan MinInterval { get; }

    /// <summary>True if this sound may play now; false if it played too recently.</summary>
    public bool TryPlay(string key)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_lastPlayed.TryGetValue(key, out var last) && now - last < MinInterval)
                return false;

            _lastPlayed[key] = now;
            return true;
        }
    }

    public void Reset()
    {
        lock (_gate) _lastPlayed.Clear();
    }
}
