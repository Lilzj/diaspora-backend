using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Observability;
using MediatR;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Counts negotiations and reports the seller's responsiveness.
/// <para>
/// <b>The response measurement is the one number here that changes behaviour.</b> A marketplace
/// where offers rise and answers do not is one where buyers are being ignored at scale, and it is
/// invisible from any infrastructure dashboard. It is recorded when a seller first moves, which is
/// the only moment the module can honestly measure.
/// </para>
/// <para>
/// The listing's offer count is adjusted here rather than inside the offer aggregate, because a
/// negotiation and a listing are separate aggregates and a write that spanned them would be the
/// cross-aggregate write the separation exists to avoid.
/// </para>
/// </summary>
public sealed class OfferEventHandler :
    INotificationHandler<DomainEventNotification<OfferCreatedDomainEvent>>,
    INotificationHandler<DomainEventNotification<OfferCounteredDomainEvent>>,
    INotificationHandler<DomainEventNotification<OfferSettledDomainEvent>>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingOfferRepository _offers;
    private readonly IListingAnalyticsRepository _analytics;

    public OfferEventHandler(
        MarketplaceIntegrationEventRelay relay,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingOfferRepository offers,
        IListingAnalyticsRepository analytics)
    {
        _relay = relay;
        _listings = listings;
        _sellers = sellers;
        _offers = offers;
        _analytics = analytics;
    }

    public async Task Handle(
        DomainEventNotification<OfferCreatedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        await _relay.PublishAsync(domainEvent, cancellationToken);

        MarketplaceTelemetry.OffersMade.Add(1);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        listing?.ApplyOfferDelta(1);

        var analytics = await _analytics.GetForListingAsync(
            domainEvent.ListingId, cancellationToken);

        analytics?.RecordOffer(domainEvent.CreatedAt);
    }

    /// <summary>
    /// A counter. Publishes nothing on its own — the settlement contracts are what consumers
    /// subscribe to, and a bus message per round of haggling would be noise nobody acts on.
    /// </summary>
    public async Task Handle(
        DomainEventNotification<OfferCounteredDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        if (domainEvent.CounteredBy != OfferParty.Seller)
        {
            return;
        }

        await RecordSellerResponseAsync(
            domainEvent.OfferId, domainEvent.SellerId, domainEvent.CounteredAt, cancellationToken);
    }

    public async Task Handle(
        DomainEventNotification<OfferSettledDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        var offer = await _offers.GetAsync(domainEvent.OfferId, cancellationToken);

        await _relay.PublishAsync(
            domainEvent, offer?.Rounds ?? 0, reservationId: null, cancellationToken);

        MarketplaceTelemetry.NegotiationHours.Record(domainEvent.Duration.TotalHours);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        listing?.ApplyOfferDelta(-1);

        if (domainEvent.Status != OfferStatus.Accepted)
        {
            return;
        }

        MarketplaceTelemetry.OffersAccepted.Add(1);

        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);
        seller?.RecordAcceptedOffer();
    }

    /// <summary>
    /// Records how long the seller took to answer, once per negotiation.
    /// <para>
    /// The aggregate's own <c>HasSellerResponded</c> flag is what makes it once: a seller who
    /// counters four times has answered once as far as responsiveness is concerned, and averaging
    /// all four would flatter them.
    /// </para>
    /// </summary>
    private async Task RecordSellerResponseAsync(
        Domain.Identifiers.ListingOfferId offerId,
        Domain.Identifiers.SellerProfileId sellerId,
        DateTimeOffset respondedAt,
        CancellationToken cancellationToken)
    {
        var offer = await _offers.GetAsync(offerId, cancellationToken);

        if (offer is null || offer.Rounds > 2)
        {
            return;
        }

        var seller = await _sellers.GetAsync(sellerId, cancellationToken);

        seller?.RecordResponse(respondedAt - offer.CreatedAt);
    }
}
