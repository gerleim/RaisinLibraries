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

    /// <summary>
    /// Seconds per refresh of the display the scrolled window is on, so repaints can be capped
    /// at what that panel can show. Zero, the default, means no cap.
    /// </summary>
    /// <remarks>
    /// WPF does not pace a window to the panel it occupies: a window moved from a fast display
    /// to a slow one keeps composing at the fast rate, so repainting once per composed frame
    /// draws several frames for every one the panel shows. The offset still decays every
    /// frame, so the motion is unchanged - only the repaint is skipped.
    ///
    /// Set by the host from its window's WindowDisplayInfo, and set again when that reports a
    /// change, so a window dragged to another monitor mid-animation is honoured on the next
    /// frame. Deliberately a value rather than a callback: asking the display a question is
    /// not this class's job, a callback would be a pull for something that changes by push,
    /// and one capturing a window is how a long-lived scroller keeps that window alive.
    /// </remarks>
    public double DisplayPeriod { get; set; }

    private double _sinceDisplayFrame;

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

        _sinceDisplayFrame = DisplayPeriod;   // let the first frame paint at once
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
        bool duplicate = false;

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
                // at the display's rate. Skipping the repaint leaves one per composed frame,
                // and that repaint drives the next frame.
                //
                // Skipping the repaint, though - not the whole callback. Returning here meant
                // an animation whose last frame happened to be a duplicate never reached
                // CheckStop: it stayed marked as animating for ever, so Start no-opped from
                // then on and the scroll silently stopped animating until something called
                // Cancel. Whether a frame is new decides what to redraw, never whether the
                // animation is finished.
                elapsed = (args.RenderingTime - _lastRenderingTime).TotalSeconds;
                if (elapsed <= 0)
                {
                    duplicate = true;
                }
                else
                {
                    _lastRenderingTime = args.RenderingTime;
                    if (elapsed < 0.5)
                        ApplyDecayWithDelay(elapsed);
                }
            }
        }

        // Always evaluated, on duplicate frames too, so settling can never be missed.
        bool stopped = CheckStop();

        if (!duplicate || stopped)
            Frame?.Invoke(duplicate || !measured ? 0 : elapsed, stopped);

        // The settled position is always drawn. It is the frame that stays on screen, and
        // with a cap in place the frames before it may well have been skipped.
        if (stopped)
        {
            _invalidateVisual();
            return;
        }

        if (duplicate)
            return;

        // Capped at what the panel can show. Skipping the repaint does not stall the loop:
        // Rendering keeps firing while the handler is attached, which the wheel path relies
        // on already - 3710 callbacks against 1236 repaints in a measured 21 second gesture.
        // The period is subtracted rather than zeroed to keep the average exact, and arrears
        // are clamped so a slow patch cannot be followed by a burst.
        _sinceDisplayFrame += elapsed;
        double period = DisplayPeriod;
        if (period > 0)
        {
            if (_sinceDisplayFrame < period)
                return;

            _sinceDisplayFrame -= period;
            if (_sinceDisplayFrame > period)
                _sinceDisplayFrame = period;
        }

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
