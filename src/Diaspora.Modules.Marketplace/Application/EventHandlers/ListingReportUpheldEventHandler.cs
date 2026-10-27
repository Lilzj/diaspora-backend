using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Sellers;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Tells the platform an actioned report was upheld, so Trust pays the reporter (REP-13). A
/// dismissed report says nothing. The seller's owner is read here so Trust can refuse a self-report.
/// </summary>
public sealed class ListingReportUpheldEventHandler : DomainEventHandler<ListingReportResolvedDomainEvent>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly ISellerProfileRepository _sellers;

    public ListingReportUpheldEventHandler(
        MarketplaceIntegrationEventRelay relay, ISellerProfileRepository sellers)
    {
        _relay = relay;
        _sellers = sellers;
    }

    protected override async Task HandleAsync(
        ListingReportResolvedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.Status != ListingReportStatus.Actioned)
        {
            return;
        }

        var seller = await _sellers.GetForReadAsync(domainEvent.SellerId, cancellationToken);

        await _relay.PublishReportUpheldAsync(domainEvent, seller?.OwnerUserId.Value, cancellationToken);
    }
}
