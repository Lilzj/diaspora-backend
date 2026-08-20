using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Relays a country directory changing shape.
/// <para>
/// The cache-invalidation event for every consumer holding a rendered country page — a CDN edge, a
/// mobile client's offline shelf, a future recommendation model.
/// </para>
/// </summary>
public sealed class CountryDirectoryChangedEventHandler
    : DomainEventHandler<CountryDirectoryChangedDomainEvent>
{
    private readonly KnowledgeIntegrationEventRelay _relay;

    public CountryDirectoryChangedEventHandler(KnowledgeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CountryDirectoryChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
