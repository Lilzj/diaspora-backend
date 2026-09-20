using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// A property was written down: publish the creation contract, and count it.
/// <para>
/// <b>A draft is not on the market, and it is still one of the landlord's properties.</b> This
/// handler used to relay the contract and nothing else, so <c>TotalListings</c> did not move until
/// the property was published or changed — a landlord with four drafts had a total of nought. The
/// refresher writes both figures and the distinction lives in the two counts it reads, not in which
/// events are allowed to reach it.
/// </para>
/// </summary>
public sealed class HousingListingCreatedEventHandler
    : DomainEventHandler<HousingListingCreatedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;
    private readonly LandlordListingCountRefresher _counts;

    public HousingListingCreatedEventHandler(
        HousingIntegrationEventRelay relay, LandlordListingCountRefresher counts)
    {
        _relay = relay;
        _counts = counts;
    }

    protected override async Task HandleAsync(
        HousingListingCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await _counts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
    }
}
