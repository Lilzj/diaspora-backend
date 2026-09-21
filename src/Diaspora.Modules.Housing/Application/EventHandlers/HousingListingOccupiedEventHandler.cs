using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Observability;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Somebody moved in: publish the contract, record the time-to-let, refresh the neighbourhood.
/// <para>
/// <b>This is the module's success path, and the one that feeds every neighbourhood market
/// statistic.</b> The time-to-let is computable exactly once — here, from a publication date the
/// listing still holds — which is why the histogram is recorded from the event rather than
/// reconstructed later from two timestamps that may both have moved.
/// </para>
/// <para>
/// The neighbourhood count is refreshed because an occupied property is no longer available: a
/// directory that still advertised it would send members to a home somebody else is living in.
/// That sentence was true here and nowhere else — the same count stayed stale when a property was
/// archived, hidden or deleted, which <c>NeighborhoodListingCountRefresher</c> now closes.
/// </para>
/// </summary>
public sealed class HousingListingOccupiedEventHandler
    : DomainEventHandler<HousingListingOccupiedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;
    private readonly NeighborhoodListingCountRefresher _neighborhoodCounts;

    public HousingListingOccupiedEventHandler(
        HousingIntegrationEventRelay relay,
        NeighborhoodListingCountRefresher neighborhoodCounts)
    {
        _relay = relay;
        _neighborhoodCounts = neighborhoodCounts;
    }

    protected override async Task HandleAsync(
        HousingListingOccupiedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (domainEvent.TimeToLet is { } timeToLet)
        {
            HousingTelemetry.TimeToLetDays.Record(timeToLet.TotalDays);
        }

        await _neighborhoodCounts.RefreshAsync(domainEvent.ListingId, cancellationToken);
    }
}
