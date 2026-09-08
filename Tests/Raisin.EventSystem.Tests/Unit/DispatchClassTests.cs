using FluentAssertions;
using Raisin.EventSystem;
using Raisin.EventSystem.Tests.Helpers;
using Xunit;

namespace Raisin.EventSystem.Tests.Unit;

[Trait("Category", "Unit")]
public class DispatchClassTests : IDisposable
{
    private readonly EventSystem _es = new();

    public void Dispose() => _es.Dispose();

    /// <summary>Records the thread each callback ran on, which is the whole subject here.</summary>
    private class ThreadRecorder : IEventSubscriber<LogArgs>
    {
        private readonly TaskCompletionSource<int> _ran = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int? ThreadId { get; private set; }
        public Task<int> Ran => _ran.Task;

        public void ExecuteEvent(object sender, LogArgs eventArgs)
        {
            ThreadId = Environment.CurrentManagedThreadId;
            _ran.TrySetResult(ThreadId.Value);
        }

        public void DestroySubscriber() { }
    }

    /// <summary>A context that records what was posted to it instead of running it.</summary>
    private class RecordingContext : SynchronizationContext
    {
        public int Posted { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Posted++;
            d(state);
        }
    }

    [Fact]
    public void State_runs_inline_on_the_raising_thread()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var recorder = new ThreadRecorder();
        _es.Subscribe(recorder, DispatchClass.State);

        _es.Invoke(this, new LogArgs("x"));

        recorder.ThreadId.Should().Be(Environment.CurrentManagedThreadId);
    }

    // The point of declaring it: a context in scope no longer decides where the callback runs.
    [Fact]
    public void State_runs_inline_even_with_a_context_in_scope()
    {
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var recorder = new ThreadRecorder();
            _es.Subscribe(recorder, DispatchClass.State);

            _es.Invoke(this, new LogArgs("x"));

            recorder.ThreadId.Should().Be(Environment.CurrentManagedThreadId);
            context.Posted.Should().Be(0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(null);
        }
    }

    // Not asserted by thread identity: the test itself runs on a pool thread, so Task.Run is entitled
    // to reuse it. What must hold is that the raiser is not made to wait, which a blocking subscriber
    // demonstrates without depending on which thread anything landed on.
    [Fact]
    public void Pool_does_not_make_the_raising_thread_wait()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        _es.Subscribe(new BlockingSubscriber(entered, release), DispatchClass.Pool);

        var raising = System.Diagnostics.Stopwatch.StartNew();
        _es.Invoke(this, new LogArgs("x"));
        raising.Stop();

        entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
            .Should().BeTrue("the callback should have been scheduled");
        raising.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "Invoke returned while the subscriber was still blocked, so it did not run inline");
        release.Set();
    }

    [Fact]
    public void State_does_make_the_raising_thread_wait()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(true);
        _es.Subscribe(new BlockingSubscriber(entered, release), DispatchClass.State);

        _es.Invoke(this, new LogArgs("x"));

        entered.IsSet.Should().BeTrue("State runs before Invoke returns, which is the point of it");
    }

    private class BlockingSubscriber(ManualResetEventSlim entered, ManualResetEventSlim release)
        : IEventSubscriber<LogArgs>
    {
        public void ExecuteEvent(object sender, LogArgs eventArgs)
        {
            entered.Set();
            // Bounded so an inline dispatch fails the test slowly rather than hanging it.
            release.Wait(TimeSpan.FromSeconds(5));
        }

        public void DestroySubscriber() { }
    }

    [Fact]
    public void Notify_posts_to_the_context_captured_at_subscribe()
    {
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        var capture = new EventCapture<LogArgs>();
        _es.Subscribe(capture, DispatchClass.Notify);
        SynchronizationContext.SetSynchronizationContext(null);

        _es.Invoke(this, new LogArgs("x"));

        context.Posted.Should().Be(1);
        capture.Received.Should().ContainSingle();
    }

    // Falling back to inline is what the legacy path does; for a declared Notify it would be a
    // subscriber quietly running somewhere it said it must not.
    [Fact]
    public void Notify_without_a_context_is_refused_rather_than_run_inline()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var capture = new EventCapture<LogArgs>();
        Exception? failure = null;
        _es.OnError = ex => failure = ex;
        _es.Subscribe(capture, DispatchClass.Notify);

        _es.Invoke(this, new LogArgs("x"));

        capture.Received.Should().BeEmpty();
        failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain(nameof(DispatchClass.Notify));
    }

    [Fact]
    public void An_undeclared_subscriber_keeps_posting_to_its_ambient_context()
    {
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        var capture = new EventCapture<LogArgs>();
        _es.Subscribe(capture);
        SynchronizationContext.SetSynchronizationContext(null);

        _es.Invoke(this, new LogArgs("x"));

        context.Posted.Should().Be(1);
    }

    [Fact]
    public void An_undeclared_subscriber_with_no_ambient_context_keeps_running_inline()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var recorder = new ThreadRecorder();
        _es.Subscribe(recorder);

        _es.Invoke(this, new LogArgs("x"));

        recorder.ThreadId.Should().Be(Environment.CurrentManagedThreadId);
    }

    [Fact]
    public void Declared_and_undeclared_subscribers_coexist_on_one_event()
    {
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        var legacy = new EventCapture<LogArgs>();
        _es.Subscribe(legacy);
        var state = new ThreadRecorder();
        _es.Subscribe(state, DispatchClass.State);
        SynchronizationContext.SetSynchronizationContext(null);

        _es.Invoke(this, new LogArgs("x"));

        context.Posted.Should().Be(1, "only the undeclared one is marshalled");
        state.ThreadId.Should().Be(Environment.CurrentManagedThreadId);
    }

    [Fact]
    public void SubscribeAll_can_state_a_class_for_every_interface()
    {
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var multi = new MultiRecorder();
            _es.SubscribeAll(multi, DispatchClass.State);

            _es.Invoke(this, new LogArgs("x"));
            _es.Invoke(this, new AlertArgs(AlertType.System, "y"));

            multi.Logs.Should().Be(1);
            multi.Alerts.Should().Be(1);
            context.Posted.Should().Be(0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(null);
        }
    }

    private class MultiRecorder : IEventSubscriber<LogArgs>, IEventSubscriber<AlertArgs>
    {
        public int Logs { get; private set; }
        public int Alerts { get; private set; }

        public void ExecuteEvent(object sender, LogArgs eventArgs) => Logs++;
        public void ExecuteEvent(object sender, AlertArgs eventArgs) => Alerts++;

        void IEventSubscriber<LogArgs>.DestroySubscriber() { }
        void IEventSubscriber<AlertArgs>.DestroySubscriber() { }
    }
}
