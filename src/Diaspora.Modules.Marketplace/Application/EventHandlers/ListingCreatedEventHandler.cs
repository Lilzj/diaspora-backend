using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Events;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// A listing was drafted: publish the contract, and nothing else.
/// <para>
/// <b>Deliberately does not index and deliberately does not count.</b> A draft is not for sale — it
/// has no photographs yet and possibly no price, and only its author can see it. Indexing here
/// would put something buyers cannot buy into search results, and counting it would inflate every
/// category tile with listings nobody ever published.
/// </para>
/// </summary>
public sealed class ListingCreatedEventHandler : DomainEventHandler<ListingCreatedDomainEvent>
{
    private readonly MarketplaceIntegrationEventRelay _relay;

    public ListingCreatedEventHandler(MarketplaceIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ListingCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
