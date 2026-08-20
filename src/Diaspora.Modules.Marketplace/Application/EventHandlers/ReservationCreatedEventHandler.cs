using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Observability;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// A hold was taken: publish the contract and count it.
/// <para>
/// <b>The stock is already held by the time this runs</b>, inside the command's own unit of work. A
/// handler that took the stock would leave a window in which the reservation exists and the units do
/// not — and a second buyer would take the same item through it.
/// </para>
/// </summary>
public sealed class ReservationCreatedEventHandler
    : DomainEventHandler<ReservationCreatedDomainEvent>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IListingAnalyticsRepository _analytics;

    public ReservationCreatedEventHandler(
        MarketplaceIntegrationEventRelay relay, IListingAnalyticsRepository analytics)
    {
        _relay = relay;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        ReservationCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        MarketplaceTelemetry.ReservationsCreated.Add(1);

        var analytics = await _analytics.GetForListingAsync(
            domainEvent.ListingId, cancellationToken);

        analytics?.RecordReservation(domainEvent.ReservedAt);
    }
}
