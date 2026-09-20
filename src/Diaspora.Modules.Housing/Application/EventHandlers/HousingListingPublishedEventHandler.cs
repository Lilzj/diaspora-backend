using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// A property went on the market: publish the contract, refresh the landlord's counts, and refresh
/// the neighbourhood's.
/// <para>
/// <b>The landlord's counts are no longer this handler's private business.</b> Publication was the
/// only moment they were ever recomputed, so a landlord who archived, let or deleted a property
/// kept the figure they had at their last publication until they published again — see
/// <c>LandlordListingCountRefresher</c> and the handlers that now call it on the other moves.
/// </para>
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
    private readonly LandlordListingCountRefresher _counts;
    private readonly INeighborhoodProfileRepository _profiles;

    public HousingListingPublishedEventHandler(
        HousingIntegrationEventRelay relay,
        IHousingListingRepository listings,
        LandlordListingCountRefresher counts,
        INeighborhoodProfileRepository profiles)
    {
        _relay = relay;
        _listings = listings;
        _counts = counts;
        _profiles = profiles;
    }

    protected override async Task HandleAsync(
        HousingListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await _counts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
        await RefreshNeighborhoodAsync(domainEvent, cancellationToken);
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
