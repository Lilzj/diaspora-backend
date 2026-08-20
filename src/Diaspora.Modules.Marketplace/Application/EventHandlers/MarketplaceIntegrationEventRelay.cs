using Diaspora.Contracts.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Turns B10's domain events into the published integration events other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B10's domain stays private.</b> A domain event
/// carries strongly typed identifiers and domain enums; an integration event carries flat
/// primitives, so a consumer can parse it without referencing this module. Doing the translation in
/// a single class means the two never drift, and means a consumer's contract cannot be changed by a
/// refactor inside an aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time this
/// runs; throwing here would roll back a completed sale because a notification did not go out.
/// Failures are logged, and at-least-once delivery is the contract — consumers treat
/// <c>EventId</c> as the idempotency key.
/// </para>
/// <para>
/// <b>Publication, sale and offer creation read the aggregates back.</b> Their contracts carry the
/// seller's name, the badge and the card payload, because the consumers that matter — a search
/// index, a standing search, a seller notification — need all of it and would otherwise each call
/// back into B10 to get it. The read is inline, inside the same unit of work that raised the event,
/// so it is the identity map rather than a round trip.
/// </para>
/// <para>
/// <b>Two domain events are deliberately never published.</b> <c>ListingSavedDomainEvent</c> is the
/// most behaviourally revealing thing this module knows about a member and is of no use to any other
/// module; <c>ConversationRequestedDomainEvent</c> is consumed in-process by the broker and says
/// nothing a bus subscriber could act on without owning messaging.
/// </para>
/// </summary>
public sealed class MarketplaceIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly ILogger<MarketplaceIntegrationEventRelay> _logger;

    public MarketplaceIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        ILogger<MarketplaceIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _listings = listings;
        _sellers = sellers;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Listing lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Task PublishAsync(
        ListingCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ListingCreatedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                CreatedBy = domainEvent.CreatedBy.Value,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                CategoryKey = domainEvent.CategoryKey,
                CountryCode = domainEvent.CountryCode,
                CreatedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// The module's most consequential contract. Reads the listing and seller back so the payload
    /// carries everything a card-level index needs.
    /// </summary>
    public async Task PublishAsync(
        ListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);

        await PublishSafelyAsync(
            new ListingPublishedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                SellerName = seller?.Identity.DisplayName ?? string.Empty,
                SellerType = (seller?.SellerType ?? SellerType.Individual).ToString(),
                IsSellerVerified = seller?.IsVerified ?? false,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                Summary = listing?.Description.Summary,
                CategoryKey = domainEvent.CategoryKey,
                Tags = listing is null ? [] : [.. listing.Tags.Select(tag => tag.Slug)],
                ListingType = (listing?.ListingType ?? ListingType.FixedPrice).ToString(),
                Condition = (listing?.Condition ?? ProductCondition.NotApplicable).ToString(),
                IsPriceDisclosed = listing?.Price.IsDisclosed ?? false,
                PriceAmount = listing?.Price.Amount,
                CurrencyCode = listing?.Price.CurrencyCode,
                IsNegotiable = listing?.Price.IsNegotiable ?? false,
                Quantity = listing?.Inventory.Available ?? 1,
                CountryCode = domainEvent.CountryCode,
                RegionName = listing?.Location.RegionName,
                CityName = listing?.Location.CityName,
                OffersPickup = listing?.Fulfillment.OffersPickup ?? false,
                OffersDelivery = listing?.Fulfillment.OffersDelivery ?? false,
                OffersShipping = listing?.Fulfillment.OffersShipping ?? false,
                CoverMediaId = listing?.CoverImage?.MediaId,
                ExpiresAt = listing?.ExpiresAt,
                IsFirstPublication = domainEvent.IsFirstPublication,
                PublishedAt = domainEvent.PublishedAt,

                // The member behind the profile. Already loaded above for the name and the badge, so
                // this costs nothing, and without it a seller-scoped consumer has a profile
                // identifier and no way to reach the person it belongs to.
                SellerUserId = seller?.OwnerUserId.Value
            },
            cancellationToken);
    }

    /// <summary>
    /// A content change, with the price where the price is what moved.
    /// <para>
    /// The previous price is passed in by the handler rather than read back, because by the time
    /// this runs the aggregate already holds the new one — the old figure exists only in the
    /// handler's own memory, and a price-drop notification is useless without it.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        ListingUpdatedDomainEvent domainEvent,
        decimal? previousPrice = null,
        decimal? currentPrice = null,
        string? currencyCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ListingUpdatedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedFields = domainEvent.ChangedFields,
                PriceAmount = currentPrice,
                PreviousPriceAmount = previousPrice,
                CurrencyCode = currencyCode,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A lifecycle transition, translated into whichever published contract it corresponds to.
    /// <para>
    /// <b>This is why the aggregate raises one status-changed event rather than seven.</b> The
    /// mapping from "moved to Archived" to "publish ListingArchived" lives here, so the domain never
    /// learns what the wire contracts are called. Forward progress through the lifecycle — Draft to
    /// Published, Active to Reserved — publishes nothing on its own: the reservation and sale
    /// contracts already report it, and a second event saying the same thing would double-count
    /// every consumer's metrics.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        ListingStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return domainEvent.CurrentStatus switch
        {
            ListingStatus.Archived => PublishSafelyAsync(
                new ListingArchivedIntegrationEvent
                {
                    ListingId = domainEvent.ListingId.Value,
                    SellerId = domainEvent.SellerId.Value,
                    ArchivedBy = domainEvent.ChangedBy?.Value,
                    Reason = domainEvent.Reason,
                    ArchivedAt = domainEvent.ChangedAt
                },
                cancellationToken),

            // An expiry de-indexes exactly as an archival does, and a consumer that had to
            // distinguish them would be one that keeps expired listings in a search index.
            ListingStatus.Expired => PublishSafelyAsync(
                new ListingArchivedIntegrationEvent
                {
                    ListingId = domainEvent.ListingId.Value,
                    SellerId = domainEvent.SellerId.Value,
                    ArchivedBy = null,
                    Reason = "expired",
                    ArchivedAt = domainEvent.ChangedAt
                },
                cancellationToken),

            _ => Task.CompletedTask
        };
    }

    public Task PublishAsync(
        ListingDeletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ListingDeletedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                DeletedBy = null,
                DeletedAt = domainEvent.DeletedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ListingRestoredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ListingRestoredIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                WasDeleted = domainEvent.WasDeleted,
                RestoredBy = null,
                RestoredAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        ListingSoldDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        await PublishSafelyAsync(
            new ListingSoldIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                BuyerUserId = domainEvent.BuyerUserId?.Value,
                ReservationId = domainEvent.ReservationId?.Value,
                Quantity = domainEvent.Quantity,
                Amount = domainEvent.Amount,
                CurrencyCode = domainEvent.CurrencyCode,
                CategoryKey = listing?.CategoryKey ?? string.Empty,
                CountryCode = listing?.Location.CountryCode ?? string.Empty,
                IsSoldOut = domainEvent.IsSoldOut,
                TimeToSale = domainEvent.TimeToSale,
                SoldAt = domainEvent.SoldAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ListingFeaturedDomainEvent domainEvent,
        Guid? promotionId = null,
        string? promotionKind = null,
        DateTimeOffset? endsAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ListingFeaturedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                IsFeatured = domainEvent.IsFeatured,
                PromotionId = promotionId,
                PromotionKind = promotionKind,
                EndsAt = endsAt,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ListingMediaAttachedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ListingMediaAttachedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                MediaId = domainEvent.MediaId,
                Role = domainEvent.Role.ToString(),
                AttachedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Seller
    // ════════════════════════════════════════════════════════════════════════════════════════

    public async Task PublishAsync(
        SellerVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);

        await PublishSafelyAsync(
            new SellerVerifiedIntegrationEvent
            {
                SellerId = domainEvent.SellerId.Value,
                OwnerUserId = seller?.OwnerUserId.Value ?? Guid.Empty,
                SellerName = seller?.Identity.DisplayName ?? string.Empty,
                PreviousState = domainEvent.PreviousState.ToString(),
                CurrentState = domainEvent.CurrentState.ToString(),
                IsVerified = domainEvent.CurrentState == ListingVerificationState.Verified,
                SellerType = (seller?.SellerType ?? SellerType.Individual).ToString(),
                VerificationId = domainEvent.VerificationId,
                Badge = domainEvent.Badge,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Negotiation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public async Task PublishAsync(
        OfferCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);

        await PublishSafelyAsync(
            new OfferCreatedIntegrationEvent
            {
                OfferId = domainEvent.OfferId.Value,
                ListingId = domainEvent.ListingId.Value,
                ListingTitle = listing?.Title.Value ?? string.Empty,
                SellerId = domainEvent.SellerId.Value,
                SellerUserId = seller?.OwnerUserId.Value ?? Guid.Empty,
                BuyerUserId = domainEvent.BuyerUserId.Value,
                Amount = domainEvent.Amount,
                CurrencyCode = domainEvent.CurrencyCode,
                Quantity = domainEvent.Quantity,
                AskingPrice = listing?.Price.Amount,
                ExpiresAt = domainEvent.ExpiresAt,
                CreatedAt = domainEvent.CreatedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A negotiation ending, split into whichever published contract it corresponds to.
    /// <para>
    /// Acceptance and refusal are separate contracts because they have separate audiences: a
    /// consumer that only cares about deals need not subscribe to every rejection on the platform to
    /// find them, and a discovery consumer wanting to say "here are similar items" needs exactly the
    /// rejections. Withdrawal and expiry go out on the refusal contract with their own status, so
    /// nothing is silently dropped.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        OfferSettledDomainEvent domainEvent,
        int rounds,
        Guid? reservationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.Status == OfferStatus.Accepted)
        {
            var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

            await PublishSafelyAsync(
                new OfferAcceptedIntegrationEvent
                {
                    OfferId = domainEvent.OfferId.Value,
                    ListingId = domainEvent.ListingId.Value,
                    ListingTitle = listing?.Title.Value ?? string.Empty,
                    SellerId = domainEvent.SellerId.Value,
                    BuyerUserId = domainEvent.BuyerUserId.Value,
                    ReservationId = reservationId,
                    Amount = domainEvent.Amount ?? 0m,
                    CurrencyCode = domainEvent.CurrencyCode,
                    Quantity = domainEvent.Quantity,
                    AcceptedByParty = domainEvent.SettledBy == domainEvent.BuyerUserId
                        ? nameof(OfferParty.Buyer)
                        : nameof(OfferParty.Seller),
                    AcceptedBy = domainEvent.SettledBy?.Value,
                    Rounds = rounds,
                    AcceptedAt = domainEvent.SettledAt
                },
                cancellationToken);

            return;
        }

        await PublishSafelyAsync(
            new OfferRejectedIntegrationEvent
            {
                OfferId = domainEvent.OfferId.Value,
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                BuyerUserId = domainEvent.BuyerUserId.Value,
                Status = domainEvent.Status.ToString(),
                Amount = domainEvent.Amount,
                CurrencyCode = domainEvent.CurrencyCode,
                SettledBy = domainEvent.SettledBy?.Value,
                Reason = domainEvent.Reason,
                SettledAt = domainEvent.SettledAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Reservation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public async Task PublishAsync(
        ReservationCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);

        await PublishSafelyAsync(
            new ListingReservedIntegrationEvent
            {
                ReservationId = domainEvent.ReservationId.Value,
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                BuyerUserId = domainEvent.BuyerUserId.Value,
                Quantity = domainEvent.Quantity,
                RemainingQuantity = listing?.Inventory.Available ?? 0,
                ExpiresAt = domainEvent.ExpiresAt,
                ReservedAt = domainEvent.ReservedAt,

                // See the publication contract's note. The read is the identity map inside the same
                // unit of work that raised the event, not a round trip.
                SellerUserId = seller?.OwnerUserId.Value
            },
            cancellationToken);
    }

    /// <summary>
    /// A reservation ending without a sale. Completion is deliberately not published from here —
    /// the listing's own sold event reports it, and publishing both would count one hand-off twice.
    /// </summary>
    public async Task PublishAsync(
        ReservationStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentStatus is not
            (ReservationStatus.Cancelled or ReservationStatus.Expired))
        {
            return;
        }

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        await PublishSafelyAsync(
            new ReservationCancelledIntegrationEvent
            {
                ReservationId = domainEvent.ReservationId.Value,
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                BuyerUserId = domainEvent.BuyerUserId.Value,
                Quantity = domainEvent.Quantity,
                RemainingQuantity = listing?.Inventory.Available ?? 0,
                WasExpired = domainEvent.CurrentStatus == ReservationStatus.Expired,
                CancelledBy = domainEvent.ChangedBy?.Value,
                Reason = domainEvent.Reason,
                CancelledAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A report. Carries no reporter identity — see the contract, and the reason it matters.
    /// </summary>
    public Task PublishAsync(
        ListingReportedDomainEvent domainEvent,
        bool wasFlagged,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ListingReportedIntegrationEvent
            {
                ReportId = domainEvent.ReportId.Value,
                ListingId = domainEvent.ListingId.Value,
                SellerId = domainEvent.SellerId.Value,
                Reason = domainEvent.Reason.ToString(),
                TotalReports = domainEvent.TotalReports,
                WasFlagged = wasFlagged,
                ReportedAt = domainEvent.ReportedAt
            },
            cancellationToken);
    }

    private async Task PublishSafelyAsync<TEvent>(
        TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed publish must not roll back a successful write.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Publishing {EventType} failed. The write succeeded; the notification did not.",
                typeof(TEvent).Name);
        }
#pragma warning restore CA1031
    }
}
