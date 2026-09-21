using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Events;

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
/// <b>And neither is the neighbourhood's.</b> That count was recomputed here and when a property
/// was let, and nowhere else — so archiving, withdrawing or deleting a home left the directory
/// advertising it. The reason this handler already gave for keeping it fresh, that a directory
/// which still advertised a let property would send members to a home somebody else is living in,
/// is the same reason it has to fall when one is withdrawn. See
/// <c>NeighborhoodListingCountRefresher</c>.
/// </para>
/// <para>
/// Both counts are recomputed rather than incremented. B1.1's rule for an at-least-once world
/// taken as written: an increment applied twice on a redelivery drifts upward forever, and a
/// neighbourhood claiming forty properties when it has eleven is a number members would act on.
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
    private readonly LandlordListingCountRefresher _landlordCounts;
    private readonly NeighborhoodListingCountRefresher _neighborhoodCounts;

    public HousingListingPublishedEventHandler(
        HousingIntegrationEventRelay relay,
        LandlordListingCountRefresher landlordCounts,
        NeighborhoodListingCountRefresher neighborhoodCounts)
    {
        _relay = relay;
        _landlordCounts = landlordCounts;
        _neighborhoodCounts = neighborhoodCounts;
    }

    protected override async Task HandleAsync(
        HousingListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await _landlordCounts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
        await _neighborhoodCounts.RefreshAsync(domainEvent.ListingId, cancellationToken);
    }
}
