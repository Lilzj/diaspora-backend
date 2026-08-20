using Diaspora.Contracts.Events;

namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// Where a message actually goes once the outbox has decided to deliver it.
/// <para>
/// <b>The seam that makes the outbox transport-agnostic.</b> Today the only implementation
/// hands the event to the in-process dispatcher, which is correct for a modular monolith and
/// keeps every existing consumer working untouched. The day a module is extracted, a broker
/// client implements this one interface and nothing above it changes: not the writer, not the
/// retry policy, not the dead-letter handling, not a single call site in a business module.
/// </para>
/// <para>
/// Implementations must throw on failure. Returning quietly tells the dispatcher the message
/// was delivered, and the dispatcher believes it — which turns a broker outage into silent
/// data loss with a green dashboard.
/// </para>
/// </summary>
public interface IOutboxTransport
{
    /// <summary>A short name for logs, metrics and the health payload.</summary>
    string Name { get; }

    /// <summary>
    /// Delivers one message.
    /// </summary>
    /// <param name="integrationEvent">The deserialized event.</param>
    /// <param name="context">Correlation, tenancy and delivery bookkeeping.</param>
    Task PublishAsync(
        IIntegrationEvent integrationEvent,
        OutboxPublishContext context,
        CancellationToken cancellationToken = default);
}
