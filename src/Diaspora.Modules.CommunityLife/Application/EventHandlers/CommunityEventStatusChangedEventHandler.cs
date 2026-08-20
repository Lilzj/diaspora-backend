using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// An event moving through its lifecycle.
/// <para>
/// Only archival is published from here. Publication, cancellation and completion each raise their
/// own event carrying figures this one does not have, and publishing both would count one
/// occasion twice in every consumer's metrics.
/// </para>
/// </summary>
public sealed class CommunityEventStatusChangedEventHandler
    : DomainEventHandler<CommunityEventStatusChangedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public CommunityEventStatusChangedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CommunityEventStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
