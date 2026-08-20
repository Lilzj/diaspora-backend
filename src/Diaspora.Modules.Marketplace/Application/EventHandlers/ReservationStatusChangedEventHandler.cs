using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Observability;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Settles the listing's stock when a reservation ends.
/// <para>
/// <b>The single most important handler in the module.</b> Completion converts held units to sold;
/// cancellation and expiry return them to the shelf. If this does not run, the units are neither on
/// the market nor accounted for — a seller with four items ends up with none of them buyable, and
/// nothing in the system says why.
/// </para>
/// <para>
/// <b>All three endings are handled here rather than in three handlers</b>, because the rule is one
/// rule: every exit settles the stock exactly once. Split across three classes it would be true in
/// two of them.
/// </para>
/// <para>
/// <b>The stock movement is idempotent by construction.</b> The aggregate refuses a release or a
/// settlement larger than what is held, so a redelivered event finds nothing left to move and fails
/// closed rather than inventing stock.
/// </para>
/// </summary>
public sealed class ReservationStatusChangedEventHandler
    : DomainEventHandler<ReservationStatusChangedDomainEvent>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingReservationRepository _reservations;
    private readonly IListingAnalyticsRepository _analytics;
    private readonly IListingStockGuard _stock;
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IClock _clock;
    private readonly ILogger<ReservationStatusChangedEventHandler> _logger;

    public ReservationStatusChangedEventHandler(
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingReservationRepository reservations,
        IListingAnalyticsRepository analytics,
        IListingStockGuard stock,
        MarketplaceIntegrationEventRelay relay,
        IClock clock,
        ILogger<ReservationStatusChangedEventHandler> logger)
    {
        _listings = listings;
        _sellers = sellers;
        _reservations = reservations;
        _analytics = analytics;
        _stock = stock;
        _relay = relay;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        ReservationStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Nothing to settle while the reservation is still holding: acknowledging moves it from
        // Pending to AwaitingCollection and the units stay exactly where they are.
        if (ReservationStatusTransitionsAreStillHolding(domainEvent.CurrentStatus))
        {
            await _relay.PublishAsync(domainEvent, cancellationToken);
            return;
        }

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        if (listing is null)
        {
            _logger.LogWarning(
                "Reservation {ReservationId} settled against listing {ListingId}, which no longer "
                + "exists. Its stock cannot be settled.",
                domainEvent.ReservationId.Value,
                domainEvent.ListingId.Value);

            return;
        }

        var now = _clock.UtcNow;

        switch (domainEvent.CurrentStatus)
        {
            case ReservationStatus.Completed:
                await CompleteAsync(domainEvent, listing, now, cancellationToken);
                break;

            case ReservationStatus.Cancelled:
            case ReservationStatus.Expired:
                await ReleaseAsync(domainEvent, listing, now, cancellationToken);
                break;
        }

        await _relay.PublishAsync(domainEvent, cancellationToken);
    }

    /// <summary>
    /// The hand-off happened: held units become sold units, and the listing raises its own sold
    /// event from inside the aggregate.
    /// </summary>
    private async Task CompleteAsync(
        ReservationStatusChangedDomainEvent domainEvent,
        MarketplaceListing listing,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetAsync(
            domainEvent.ReservationId, cancellationToken);

        var settled = await _stock.SettleAsync(
            listing,
            domainEvent.Quantity,
            now,
            domainEvent.BuyerUserId,
            domainEvent.ReservationId,
            reservation?.AgreedAmount,
            reservation?.CurrencyCode,
            cancellationToken);

        if (settled.IsFailure)
        {
            _logger.LogWarning(
                "Settling {Quantity} unit(s) of listing {ListingId} for reservation "
                + "{ReservationId} was refused: {Error}. The reservation is complete and the stock "
                + "was already accounted for.",
                domainEvent.Quantity,
                domainEvent.ListingId.Value,
                domainEvent.ReservationId.Value,
                settled.Error.Code);

            return;
        }

        MarketplaceTelemetry.ReservationsCompleted.Add(1);

        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);
        seller?.RecordCompletedReservation();

        var analytics = await _analytics.GetForListingAsync(
            domainEvent.ListingId, cancellationToken);

        analytics?.RecordSale(now, domainEvent.Quantity);
    }

    /// <summary>
    /// Nobody bought: the units go back on the shelf, and a listing that had gone Reserved becomes
    /// available again in the same beat.
    /// </summary>
    private async Task ReleaseAsync(
        ReservationStatusChangedDomainEvent domainEvent,
        MarketplaceListing listing,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var released = await _stock.ReleaseAsync(
            listing, domainEvent.Quantity, now, cancellationToken);

        if (released.IsFailure)
        {
            _logger.LogWarning(
                "Releasing {Quantity} unit(s) of listing {ListingId} for reservation "
                + "{ReservationId} was refused: {Error}. The stock was already released.",
                domainEvent.Quantity,
                domainEvent.ListingId.Value,
                domainEvent.ReservationId.Value,
                released.Error.Code);

            return;
        }

        MarketplaceTelemetry.ReservationsAbandoned.Add(
            1,
            new KeyValuePair<string, object?>(
                "cause",
                domainEvent.CurrentStatus == ReservationStatus.Expired ? "expired" : "cancelled"));

        // Only a cancellation counts against the seller. An expiry is a buyer who did not turn up,
        // and holding it against the person who waited would be exactly backwards.
        if (domainEvent.CurrentStatus == ReservationStatus.Cancelled)
        {
            var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);

            if (seller is not null && domainEvent.ChangedBy == seller.OwnerUserId)
            {
                seller.RecordCancelledReservation();
            }
        }
    }

    private static bool ReservationStatusTransitionsAreStillHolding(ReservationStatus status) =>
        status is ReservationStatus.Pending or ReservationStatus.AwaitingCollection;
}
