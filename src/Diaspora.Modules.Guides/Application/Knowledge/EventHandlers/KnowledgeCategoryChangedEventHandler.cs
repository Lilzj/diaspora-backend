using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Relays a navigation row being created, renamed, archived or restored.
/// <para>
/// One handler for the whole lifecycle, because the consequence is the same in every case: a
/// consumer caching navigation has to drop it. The verb on the event is there for the minority that
/// care which kind of change it was.
/// </para>
/// </summary>
public sealed class KnowledgeCategoryChangedEventHandler
    : DomainEventHandler<KnowledgeCategoryChangedDomainEvent>
{
    private readonly KnowledgeIntegrationEventRelay _relay;

    public KnowledgeCategoryChangedEventHandler(KnowledgeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        KnowledgeCategoryChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
