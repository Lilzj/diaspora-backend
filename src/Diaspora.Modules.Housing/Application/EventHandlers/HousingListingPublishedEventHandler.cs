using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// A property went on the market: publish the contract, refresh the landlord's counts, and refresh
/// the neighbourhood's.
/// <para>
/// <b>The neighbourhood listing count is recomputed rather than incremented.</b> B1.1's rule for an
/// at-least-once world taken as written: an increment applied twice on a redelivery drifts upward
/// forever, and a neighbourhood claiming forty properties when it has eleven is a number members
/// would act on. Recomputing costs one indexed count on a path that fires when a landlord
/// publishes, which is rare.
/// </para>
/// <para>
/// <b>Neither refresh may fail the publication.</b> A property that is live with a stale count
/// beside it is a cosmetic problem; a publication rolled back because a counter would not update
/// would lose the landlord's work.
/// </para>
/// </summary>
public sealed class HousingListingPublishedEventHandler
    : DomainEventHandler<HousingListingPublishedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly INeighborhoodProfileRepository _profiles;

    public HousingListingPublishedEventHandler(
        HousingIntegrationEventRelay relay,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        INeighborhoodProfileRepository profiles)
    {
        _relay = relay;
        _listings = listings;
        _landlords = landlords;
        _profiles = profiles;
    }

    protected override async Task HandleAsync(
        HousingListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await RefreshLandlordAsync(domainEvent, cancellationToken);
        await RefreshNeighborhoodAsync(domainEvent, cancellationToken);
    }

    private async Task RefreshLandlordAsync(
        HousingListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var landlord = await _landlords.GetAsync(domainEvent.LandlordId, cancellationToken);

        if (landlord is null)
        {
            return;
        }

        var active = await _listings.CountActiveByLandlordAsync(
            domainEvent.LandlordId, cancellationToken);

        landlord.RecordListingCounts(active, Math.Max(active, landlord.Statistics.TotalListings));
    }

    /// <summary>
    /// Recomputes the neighbourhood's live-listing count. See the class summary for why it is set
    /// rather than incremented.
    /// </summary>
    private async Task RefreshNeighborhoodAsync(
        HousingListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.NeighborhoodAreaId is not { } areaId || areaId == Guid.Empty)
        {
            return;
        }

        var profile = await _profiles.GetByAreaAsync(areaId, cancellationToken);

        if (profile is null)
        {
            return;
        }

        profile.SetListingCount(
            await _listings.CountInNeighborhoodAsync(areaId, cancellationToken));
    }
}
