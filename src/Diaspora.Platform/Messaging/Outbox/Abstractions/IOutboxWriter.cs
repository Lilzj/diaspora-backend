using Diaspora.Contracts.Events;

namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// Records an integration event into the caller's current unit of work.
/// <para>
/// <b>Enqueue does not save, and that is the entire design.</b> The row joins whatever
/// transaction the caller already has open, so the aggregate and the promise to publish it
/// commit together or not at all. A writer that saved on its own would reintroduce the defect
/// this phase exists to remove, one layer down: the aggregate could commit and the event could
/// still be rolled back, or the reverse.
/// </para>
/// <para>
/// Synchronous by design. There is no I/O here — the row is added to the change tracker and
/// nothing else — and an async signature would invite a call site to believe otherwise.
/// </para>
/// </summary>
public interface IOutboxWriter
{
    /// <summary>
    /// Writes the event into the current unit of work. Returns the message identifier, which
    /// is the event's own <c>EventId</c> and the key consumers deduplicate on.
    /// </summary>
    Guid Enqueue(IIntegrationEvent integrationEvent);

    /// <summary>
    /// How many messages this unit of work has enqueued and not yet saved. Read by the
    /// persistence layer to decide whether a flush is needed, and by tests to assert that a
    /// rollback took the events with it.
    /// </summary>
    int PendingCount { get; }
}
