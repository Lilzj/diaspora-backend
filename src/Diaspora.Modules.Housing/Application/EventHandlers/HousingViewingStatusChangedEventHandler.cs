using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes whichever viewing contract the transition corresponds to.
/// <para>
/// A request publishes nothing: somebody intention to visit a named address is not a fact that
/// belongs on a bus. Scheduling and every terminal state do.
/// </para>
/// </summary>
public sealed class HousingViewingStatusChangedEventHandler
    : DomainEventHandler<HousingViewingStatusChangedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingViewingStatusChangedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        HousingViewingStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
