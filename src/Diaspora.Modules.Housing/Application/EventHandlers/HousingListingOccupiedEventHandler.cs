using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
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
/// </para>
/// </summary>
public sealed class HousingListingOccupiedEventHandler
    : DomainEventHandler<HousingListingOccupiedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;
    private readonly IHousingListingRepository _listings;
    private readonly INeighborhoodProfileRepository _profiles;

    public HousingListingOccupiedEventHandler(
        HousingIntegrationEventRelay relay,
        IHousingListingRepository listings,
        INeighborhoodProfileRepository profiles)
    {
        _relay = relay;
        _listings = listings;
        _profiles = profiles;
    }

    protected override async Task HandleAsync(
        HousingListingOccupiedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (domainEvent.TimeToLet is { } timeToLet)
        {
            HousingTelemetry.TimeToLetDays.Record(timeToLet.TotalDays);
        }

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        if (listing?.Location.NeighborhoodAreaId is not { } areaId || areaId == Guid.Empty)
        {
            return;
        }

        var profile = await _profiles.GetByAreaAsync(areaId, cancellationToken);

        profile?.SetListingCount(
            await _listings.CountInNeighborhoodAsync(areaId, cancellationToken));
    }
}
