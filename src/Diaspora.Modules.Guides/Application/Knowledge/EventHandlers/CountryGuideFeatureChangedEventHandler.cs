using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Relays a guide being featured on, or removed from, a country shelf.
/// <para>
/// Country-scoped featuring, which the global flag B8 owns cannot express: the same guide is
/// legitimately the first thing to read about one country and irrelevant to the next.
/// </para>
/// </summary>
public sealed class CountryGuideFeatureChangedEventHandler
    : DomainEventHandler<CountryGuideFeatureChangedDomainEvent>
{
    private readonly KnowledgeIntegrationEventRelay _relay;

    public CountryGuideFeatureChangedEventHandler(KnowledgeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CountryGuideFeatureChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
