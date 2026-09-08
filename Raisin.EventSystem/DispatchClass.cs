namespace Raisin.EventSystem;

/// <summary>
/// Which thread a subscriber wants its callbacks on, stated by the subscriber.
/// </summary>
/// <remarks>
/// <para>
/// Without this, the answer is assembled from two accidents: the producer's choice of
/// <see cref="EventSystem.Invoke"/> or <see cref="EventSystem.InvokeOnThreadPool"/>, and whichever
/// <see cref="SynchronizationContext"/> happened to be current when the subscriber was constructed.
/// So the same subscriber runs on a different thread depending on which producer raised the event,
/// and moving a line in a constructor changes the threading of unrelated code with nothing to fail.
/// </para>
/// <para>
/// The overloads that do not take one of these keep that legacy behaviour exactly, so declaring the
/// class is a migration rather than a switch: what remains on the legacy overloads is the list of
/// subscribers nobody has decided about yet.
/// </para>
/// </remarks>
public enum DispatchClass
{
    /// <summary>
    /// Runs inline on whatever thread raised the event, always.
    /// </summary>
    /// <remarks>
    /// For state a decision reads, where being behind is a correctness fault rather than a display
    /// one, and for state that must be applied in the order it was produced. The callback is charged
    /// to the producer's thread — a socket reader, typically — so it may write a field or a
    /// dictionary and must not block, wait, or touch a UI.
    /// </remarks>
    State,

    /// <summary>
    /// Runs on a thread-pool thread, whatever thread raised the event.
    /// </summary>
    /// <remarks>
    /// For work that is neither ordered nor urgent but should not be charged to the producer: file
    /// I/O, caches, anything that can afford a queue. Ordering between events is not preserved.
    /// </remarks>
    Pool,

    /// <summary>
    /// Runs on the <see cref="SynchronizationContext"/> captured when the subscriber subscribed.
    /// </summary>
    /// <remarks>
    /// For anything a person looks at. Subscribing without a context is a mistake worth catching, so
    /// unlike the legacy path this does not silently fall back to running inline.
    /// </remarks>
    Notify,
}
