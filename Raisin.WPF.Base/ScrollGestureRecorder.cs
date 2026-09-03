using System.Diagnostics;
using System.IO;

namespace Raisin.WPF.Base;

/// <summary>
/// Records how evenly a scroll gesture's frames and pixel steps came out, to a log a host
/// names.
/// </summary>
/// <remarks>
/// Two different things read as jagged and they need telling apart. A skipped composition frame
/// is the compositor not showing one that was drawn. An uneven pixel step is the offset crossing
/// pixel boundaries at irregular intervals, which is what a decaying coast does naturally once
/// its speed drops near one pixel per frame - whole-pixel stepping, nothing to do with dropped
/// frames.
///
/// Everything here was earned by a wrong guess. Gestures were once discarded below a paint
/// count, which made "diagnostics off", "gesture too small" and "gesture never ended" identical
/// in the file and cost three test rounds; every gesture is now recorded and Summarise says
/// where the numbers do not hold. The marker written when recording starts separates a log that
/// was never switched on from one that was. And a gesture names the display it ran on rather
/// than leaving it to be inferred from the measured rate, because the failure worth measuring -
/// a window paced to the display it started on rather than the one it occupies - produces the
/// old rate on the new monitor, which is indistinguishable from not having moved.
/// </remarks>
public sealed class ScrollGestureRecorder(string logPath)
{
    private readonly List<double> _frameGaps = new(512);
    private readonly List<double> _paintGaps = new(512);
    private readonly List<double> _pixelSteps = new(512);
    private readonly Stopwatch _clock = new();
    private readonly object _costLock = new();
    private readonly Dictionary<string, (double Max, double Total, int Count)> _costs = new();

    private double _sincePaint;
    private int _frames, _paints;
    private int _gc0, _gc1, _gc2;
    private string _device = string.Empty;
    private int _hz;

    /// <summary>Whether anything is recorded. Off costs a branch per frame.</summary>
    public bool Enabled { get; set; }

    /// <summary>What kind of gesture is being recorded - "wheel", "smooth" - for the log line.</summary>
    public string Source { get; set; } = "wheel";

    /// <summary>Extra text appended to the final line of a gesture summary.</summary>
    public Func<string>? Extra { get; set; }

    /// <summary>The display a gesture is running on, named in its lines.</summary>
    public void SetDisplay(string device, int refreshRate)
    {
        _device = device;
        _hz = refreshRate;
    }

    /// <summary>Appends one stamped line, for markers outside a gesture.</summary>
    public void Note(string text)
    {
        if (!Enabled) return;
        Write($"{DateTime.Now:HH:mm:ss.fff}  {text}");
    }

    /// <summary>Times <paramref name="work"/> under <paramref name="label"/>.</summary>
    public void Time(string label, Action work)
    {
        if (!Enabled) { work(); return; }

        var sw = Stopwatch.StartNew();
        work();
        sw.Stop();

        lock (_costLock)
        {
            _costs.TryGetValue(label, out var c);
            _costs[label] = (Math.Max(c.Max, sw.Elapsed.TotalMilliseconds),
                c.Total + sw.Elapsed.TotalMilliseconds, c.Count + 1);
        }
    }

    /// <summary>One callback of the animation loop. Starts a gesture on the first call.</summary>
    public void Frame(double dt, bool newFrame)
    {
        if (!Enabled) return;

        if (!_clock.IsRunning)
        {
            _clock.Restart();
            _gc0 = GC.CollectionCount(0);
            _gc1 = GC.CollectionCount(1);
            _gc2 = GC.CollectionCount(2);
            SnapshotCosts();   // discard anything from before the gesture

            // Written as the gesture begins, not only when it ends: a gesture that starts and
            // never finishes is a failure worth seeing, and without this it looks exactly like
            // a gesture that never happened.
            Write($"{DateTime.Now:HH:mm:ss.fff}  {Source} gesture started{DisplayLabel()}");
        }

        _frames++;
        _sincePaint += dt;
        if (newFrame && dt > 0 && dt < 0.5)
            _frameGaps.Add(dt * 1000);
    }

    /// <summary>A frame that was actually drawn, and how far the view moved for it.</summary>
    public void Paint(double pixelStep)
    {
        if (!Enabled) return;

        _paints++;
        if (_sincePaint > 0 && _sincePaint < 0.5)
            _paintGaps.Add(_sincePaint * 1000);
        _sincePaint = 0;
        _pixelSteps.Add(Math.Abs(pixelStep));
    }

    /// <summary>The gesture finished: writes its summary and resets.</summary>
    public void End()
    {
        if (!Enabled) { Reset(); return; }

        double seconds = _clock.Elapsed.TotalSeconds;

        // The tail is where a coast is slowest and whole-pixel stepping shows most, so it is
        // reported on its own rather than averaged into the fast part.
        int tailFrom = Math.Max(0, _paintGaps.Count - 20);
        var tail = _paintGaps.GetRange(tailFrom, _paintGaps.Count - tailFrom);

        var steps = _pixelSteps.ToArray();
        Array.Sort(steps);
        int onePixel = 0, more = 0;
        foreach (var v in _pixelSteps)
        {
            if (v <= 1.0) onePixel++;
            else more++;
        }

        var text =
            $"{DateTime.Now:HH:mm:ss.fff}  {Source} gesture {seconds:F2}s{DisplayLabel()}  " +
            $"{_frames} ticks, {_paints} paints" + Environment.NewLine +
            $"    {Summarise(_frameGaps, "composition frame")}" + Environment.NewLine +
            $"    {Summarise(_paintGaps, "paint interval")}" + Environment.NewLine +
            $"    {Summarise(tail, "tail paint interval")}" + Environment.NewLine +
            $"    costs: {SnapshotCosts()}" + Environment.NewLine +
            $"    gc during gesture: gen0 {GC.CollectionCount(0) - _gc0}, " +
            $"gen1 {GC.CollectionCount(1) - _gc1}, gen2 {GC.CollectionCount(2) - _gc2}" +
            Environment.NewLine +
            $"    pixel steps: 1px {100.0 * onePixel / Math.Max(1, _pixelSteps.Count):F0}%, " +
            $"more {100.0 * more / Math.Max(1, _pixelSteps.Count):F0}%, " +
            $"largest {(steps.Length > 0 ? steps[^1] : 0):F0}px" +
            (Extra?.Invoke() is { Length: > 0 } extra ? "   " + extra : string.Empty);

        Write(text);
        Reset();
    }

    private string DisplayLabel() =>
        _hz > 0 ? $" on {(_device.Length > 0 ? _device : "unknown display")} {_hz}Hz" : string.Empty;

    private static string Summarise(List<double> values, string label)
    {
        if (values.Count < 4) return $"{label}: too few samples";

        var sorted = values.ToArray();
        Array.Sort(sorted);
        double median = sorted[sorted.Length / 2];

        int late = 0;
        foreach (var v in values)
            if (v > median * 1.5) late++;

        return $"{label} median {median:F2}ms ({1000 / median:F0}/s), " +
               $"p99 {sorted[(int)(sorted.Length * 0.99)]:F2}ms, max {sorted[^1]:F2}ms, " +
               $"over 1.5x median {100.0 * late / values.Count:F1}%";
    }

    private string SnapshotCosts()
    {
        lock (_costLock)
        {
            if (_costs.Count == 0) return "none recorded";

            var parts = new List<string>(_costs.Count);
            foreach (var kv in _costs)
            {
                var (max, total, count) = kv.Value;
                parts.Add($"{kv.Key} x{count} max {max:F1}ms avg {total / Math.Max(1, count):F2}ms");
            }
            _costs.Clear();

            parts.Sort(StringComparer.Ordinal);
            return string.Join(", ", parts);
        }
    }

    private void Write(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void Reset()
    {
        _frameGaps.Clear();
        _paintGaps.Clear();
        _pixelSteps.Clear();
        _clock.Reset();
        _sincePaint = 0;
        _frames = _paints = 0;
    }
}
