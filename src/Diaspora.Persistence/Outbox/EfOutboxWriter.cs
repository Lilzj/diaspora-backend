using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Telemetry;

namespace Diaspora.Persistence.Outbox;

/// <summary>
/// Adds the outbox row to the caller's own change tracker.
/// <para>
/// <b>The whole atomicity guarantee is these three lines.</b> The row is added to the same
/// <c>DbContext</c> the aggregate was mutated on, so it is written by the same
/// <c>SaveChanges</c>, inside the same transaction, and a rollback takes both. There is no
/// second connection, no distributed transaction and no compensating write — the reason the
/// transactional outbox pattern is the standard answer is that it needs none of those.
/// </para>
/// <para>
/// It deliberately does not save. A writer that saved would commit the event on its own, and
/// the failure modes would be exactly the ones B1.1 exists to remove, one layer down: an event
/// committed for an aggregate that rolled back, or an aggregate committed with its event lost
/// to a failure two statements later.
/// </para>
/// </summary>
public sealed class EfOutboxWriter : IOutboxWriter
{
    private readonly DiasporaDbContext _context;
    private readonly OutboxMessageFactory _factory;

    private int _pending;

    public EfOutboxWriter(DiasporaDbContext context, OutboxMessageFactory factory)
    {
        _context = context;
        _factory = factory;
    }

    public int PendingCount => _pending;

    public Guid Enqueue(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var message = _factory.Create(integrationEvent);

        _context.Set<OutboxMessage>().Add(message);
        _pending++;

        OutboxTelemetry.Enqueued.Add(
            1,
            new KeyValuePair<string, object?>("event.type", message.EventType),
            new KeyValuePair<string, object?>("module", message.Module));

        return message.MessageId;
    }
}
