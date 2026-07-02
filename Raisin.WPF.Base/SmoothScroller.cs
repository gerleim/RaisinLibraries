using System.Windows.Media;

namespace Raisin.WPF.Base;

public class SmoothScroller
{
    private const double Damping = 30.0;
    private const double FrameInterval = 1.0 / 60.0;
    private const double StopThreshold = 0.5;

    private bool _isAnimating;
    private TimeSpan _lastRenderingTime;
    private double _animTimeAccumulator;
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
        _animTimeAccumulator = 0;
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

        _animTimeAccumulator += decayTime * TimeScale;
        while (_animTimeAccumulator >= FrameInterval)
        {
            _animTimeAccumulator -= FrameInterval;
            Offset *= Math.Exp(-FrameInterval * Damping);
        }
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
        if (e is RenderingEventArgs args)
        {
            if (_lastRenderingTime == TimeSpan.Zero)
            {
                _lastRenderingTime = args.RenderingTime;
                if (_dampingDelayRemaining <= 0)
                    Offset *= Math.Exp(-FrameInterval * Damping);
            }
            else
            {
                double elapsed = (args.RenderingTime - _lastRenderingTime).TotalSeconds;
                _lastRenderingTime = args.RenderingTime;
                if (elapsed > 0 && elapsed < 0.5)
                    ApplyDecayWithDelay(elapsed);
            }
        }

        if (CheckStop())
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
