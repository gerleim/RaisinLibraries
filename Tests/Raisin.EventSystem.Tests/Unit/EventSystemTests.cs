using FluentAssertions;
using Raisin.EventSystem;
using Raisin.EventSystem.Tests.Helpers;
using Xunit;

namespace Raisin.EventSystem.Tests.Unit;

[Trait("Category", "Unit")]
public class EventSystemTests : IDisposable
{
    private readonly EventSystem _es = new();

    public void Dispose() => _es.Dispose();

    [Fact]
    public void Subscribe_and_invoke_delivers_event()
    {
        SynchronizationContext.SetSynchronizationContext(null);

        var capture = new EventCapture<LogArgs>();
        _es.Subscribe(capture);

        _es.Invoke(this, new LogArgs("hello"));

        capture.Received.Should().ContainSingle()
            .Which.Message.Should().Be("hello");
    }

    [Fact]
    public void SubscribeAll_registers_all_interfaces()
    {
        SynchronizationContext.SetSynchronizationContext(null);

        var multi = new MultiSubscriber();
        _es.SubscribeAll(multi);

        _es.Invoke(this, new LogArgs("msg"));
        _es.Invoke(this, new AlertArgs(AlertType.System, "alert"));

        multi.Messages.Should().ContainSingle();
        multi.Alerts.Should().ContainSingle();
    }

    [Fact]
    public async Task InvokeOnThreadPool_delivers_asynchronously()
    {
        var capture = new EventCapture<LogArgs>();
        _es.Subscribe(capture);

        _es.InvokeOnThreadPool(this, new LogArgs("async"));

        var result = await capture.WaitForEvent(TimeSpan.FromSeconds(2));
        result.Message.Should().Be("async");
    }

    [Fact]
    public void DestroySubscriber_stops_delivery()
    {
        SynchronizationContext.SetSynchronizationContext(null);

        var capture = new EventCapture<LogArgs>();
        _es.Subscribe(capture);
        _es.DestroySubscriber(capture);

        _es.Invoke(this, new LogArgs("should not arrive"));

        capture.Received.Should().BeEmpty();
    }

    [Fact]
    public void Multiple_subscribers_all_receive_event()
    {
        SynchronizationContext.SetSynchronizationContext(null);

        var capture1 = new EventCapture<LogArgs>();
        var capture2 = new EventCapture<LogArgs>();
        _es.Subscribe(capture1);
        _es.Subscribe(capture2);

        _es.Invoke(this, new LogArgs("broadcast"));

        capture1.Received.Should().ContainSingle();
        capture2.Received.Should().ContainSingle();
    }

    [Fact]
    public void Filter_only_delivers_matching_events()
    {
        SynchronizationContext.SetSynchronizationContext(null);

        var capture = new EventCapture<LogArgs>();
        _es.Subscribe(capture, (LogArgs m) => m.Message == "yes");

        _es.Invoke(this, new LogArgs("no"));
        _es.Invoke(this, new LogArgs("yes"));
        _es.Invoke(this, new LogArgs("nope"));

        capture.Received.Should().ContainSingle()
            .Which.Message.Should().Be("yes");
    }

    [Fact]
    public void No_filter_delivers_all_events()
    {
        SynchronizationContext.SetSynchronizationContext(null);

        var capture = new EventCapture<LogArgs>();
        _es.Subscribe(capture);

        _es.Invoke(this, new LogArgs("a"));
        _es.Invoke(this, new LogArgs("b"));
        _es.Invoke(this, new LogArgs("c"));

        capture.Received.Should().HaveCount(3);
    }

    [Fact]
    public void Filter_exception_does_not_block_other_subscribers()
    {
        SynchronizationContext.SetSynchronizationContext(null);

        Exception? captured = null;
        _es.OnError = ex => captured = ex;

        var capture1 = new EventCapture<LogArgs>();
        _es.Subscribe(capture1, (LogArgs _) => throw new InvalidOperationException("boom"));

        var capture2 = new EventCapture<LogArgs>();
        _es.Subscribe(capture2);

        _es.Invoke(this, new LogArgs("test"));

        capture1.Received.Should().BeEmpty();
        capture2.Received.Should().ContainSingle();
        captured.Should().BeOfType<InvalidOperationException>();
    }

    private class MultiSubscriber :
        IEventSubscriber<LogArgs>,
        IEventSubscriber<AlertArgs>
    {
        public List<LogArgs> Messages { get; } = [];
        public List<AlertArgs> Alerts { get; } = [];

        public void ExecuteEvent(object sender, LogArgs eventArgs) => Messages.Add(eventArgs);
        public void ExecuteEvent(object sender, AlertArgs eventArgs) => Alerts.Add(eventArgs);
        public void DestroySubscriber() { }
    }
}
