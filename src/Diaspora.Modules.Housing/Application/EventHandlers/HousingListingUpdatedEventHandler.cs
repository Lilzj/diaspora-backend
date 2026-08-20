using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes a property's content change — B23.x Wave 12.
/// <para>
/// <b>The handler B11 never wrote.</b> <c>HousingListingUpdatedDomainEvent</c> has been raised on
/// every edit since the first release, and nothing subscribed to it: no handler, no relay method, no
/// contract. The gap was invisible because the module's own read paths load the aggregate, so a rent
/// change was correct everywhere inside Housing and unobservable everywhere outside it.
/// </para>
/// <para>
/// A handler rather than an inline publish in the command, matching every other publication in this
/// module: the aggregate is already saved by the time this runs, and a publish failure must never
/// roll back a landlord's edit.
/// </para>
/// </summary>
public sealed class HousingListingUpdatedEventHandler
    : DomainEventHandler<HousingListingUpdatedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingListingUpdatedEventHandler(HousingIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        HousingListingUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
