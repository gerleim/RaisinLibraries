using System.Windows.Media;

namespace Raisin.WPF.Base;

public class SmoothScroller
{
    private const double Damping = 30.0;
    private const double FrameInterval = 1.0 / 60.0;
    private const double StopThreshold = 0.5;

    private bool _isAnimating;
    private TimeSpan _lastRenderingTime;
    private double _dampingDelayRemaining;
    private readonly Action _invalidateVisual;
    private readonly Func<bool> _canStop;

    public double Offset { get; set; }
    public bool IsAnimating => _isAnimating;

    public void ApplyOneFrameDecay()
    {
        Offset *= Math.Exp(-FrameInterval * Damping);
    }

    public void DeferDamping(double seconds)
    {
        _dampingDelayRemaining = seconds;
    }

    public bool ManualMode { get; set; }
    public double TimeScale { get; set; } = 1.0;

    /// <summary>
    /// Raised once per composed frame while animating: the seconds since the previous frame,
    /// and whether this frame ended the animation. The elapsed time is zero when there was no
    /// previous frame to measure against, which a subscriber should not count as a gap.
    /// </summary>
    /// <remarks>
    /// Reports the animation's cadence without the library deciding what to do with it, the
    /// same split as IDocsLogger - a host that wants to measure smoothness subscribes and
    /// writes its own log; one that does not pays an unsubscribed event check per frame.
    ///
    /// Duplicate Rendering callbacks for a frame already returned before this point, so every
    /// raise is a distinct composed frame. Only the automatic loop raises it; a host driving
    /// Step or StepRaw in ManualMode already knows its own timings.
    /// </remarks>
    public event Action<double, bool>? Frame;

    public SmoothScroller(Action invalidateVisual, Func<bool>? canStop = null)
    {
        _invalidateVisual = invalidateVisual;
        _canStop = canStop ?? (() => true);
    }

    public void Start()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        _lastRenderingTime = TimeSpan.Zero;
        if (!ManualMode)
            CompositionTarget.Rendering += OnFrame;
        _invalidateVisual();
    }

    public void Cancel()
    {
        if (!_isAnimating) return;
        Offset = 0;
        _isAnimating = false;
        if (!ManualMode)
            CompositionTarget.Rendering -= OnFrame;
    }

    public void Step(double elapsedSeconds)
    {
        if (!_isAnimating) return;
        ApplyDecayWithDelay(elapsedSeconds);
        CheckStop();
        _invalidateVisual();
    }

    private void ApplyDecayWithDelay(double elapsed)
    {
        double decayTime = elapsed;
        if (_dampingDelayRemaining > 0)
        {
            _dampingDelayRemaining -= elapsed;
            if (_dampingDelayRemaining >= 0)
                return;
            decayTime = -_dampingDelayRemaining;
            _dampingDelayRemaining = 0;
        }

        // Continuous, not quantised into sixtieths.
        //
        // This used to accumulate elapsed time and apply one 1/60 step of decay per whole
        // frame's worth, which meant Offset only changed 60 times a second however often the
        // display refreshed - so on a 280Hz panel the animation repainted at the panel's rate
        // but moved in 60 discrete jumps, which reads as stepping.
        //
        // Exponential decay composes: exp(-a) * exp(-b) == exp(-(a+b)). So decaying by the real
        // elapsed time gives exactly the same result at every 1/60 boundary the old loop landed
        // on, and a smooth value in between.
        Offset *= Math.Exp(-decayTime * TimeScale * Damping);
    }

    public void StepRaw(double elapsedSeconds)
    {
        if (!_isAnimating) return;
        Offset *= Math.Exp(-elapsedSeconds * Damping);
        CheckStop();
        _invalidateVisual();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        double elapsed = FrameInterval;

        // The priming frame has no previous stamp to measure against, so its elapsed time is
        // the assumed FrameInterval rather than anything observed. Reporting that as a frame
        // gap put a constant 16.67ms into every gesture's max and p99.
        bool measured = true;

        if (e is RenderingEventArgs args)
        {
            if (_lastRenderingTime == TimeSpan.Zero)
            {
                _lastRenderingTime = args.RenderingTime;
                if (_dampingDelayRemaining <= 0)
                    Offset *= Math.Exp(-FrameInterval * Damping);
                measured = false;
            }
            else
            {
                // RenderingTime is the composition engine's frame stamp, and it repeats when
                // Rendering fires more than once for the same frame. Invalidating on those
                // duplicates is what makes the loop free-run: the repaint schedules another
                // pass, which raises Rendering again, at several hundred a second rather than
                // at the display's rate. Returning without repainting leaves one repaint per
                // composed frame, and that repaint drives the next frame.
                elapsed = (args.RenderingTime - _lastRenderingTime).TotalSeconds;
                if (elapsed <= 0) return;

                _lastRenderingTime = args.RenderingTime;
                if (elapsed < 0.5)
                    ApplyDecayWithDelay(elapsed);
            }
        }

        bool stopped = CheckStop();
        Frame?.Invoke(measured ? elapsed : 0, stopped);
        if (stopped)
            return;

        _invalidateVisual();
    }

    private bool CheckStop()
    {
        if (_canStop() && Math.Abs(Offset) < StopThreshold)
        {
            Offset = 0;
            _isAnimating = false;
            if (!ManualMode)
                CompositionTarget.Rendering -= OnFrame;
            return true;
        }
        return false;
    }
}
