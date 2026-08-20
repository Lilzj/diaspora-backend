using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Promotions;
using Diaspora.Platform.Services.Abstractions;
using MediatR;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Keeps a listing's featured flag and its promotions in step.
/// <para>
/// <b>The flag is derived from the promotions, not set beside them.</b> That is the whole reason a
/// promotion is its own aggregate: a boolean on the listing cannot expire itself, and the one left
/// set after a window closes is the marketplace giving away placement it meant to sell.
/// </para>
/// <para>
/// <b>Ending a promotion only strips the flag when no other live promotion still grants it.</b> A
/// listing with two overlapping promotions keeps its placement until the last of them ends, and a
/// handler that did not check would strip it on the first.
/// </para>
/// </summary>
public sealed class PromotionEventHandler :
    INotificationHandler<DomainEventNotification<ListingPromotionStartedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ListingPromotionEndedDomainEvent>>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IListingPromotionRepository _promotions;
    private readonly IClock _clock;

    public PromotionEventHandler(
        IMarketplaceListingRepository listings,
        IListingPromotionRepository promotions,
        IClock clock)
    {
        _listings = listings;
        _promotions = promotions;
        _clock = clock;
    }

    public async Task Handle(
        DomainEventNotification<ListingPromotionStartedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        if (!GrantsPlacement(domainEvent.Kind))
        {
            return;
        }

        var now = _clock.UtcNow;

        // A promotion scheduled to start later grants nothing yet. The sweep is not what turns it
        // on — the next pass over an already-started window is, and until then the listing is
        // exactly as it was.
        if (domainEvent.StartsAt > now)
        {
            return;
        }

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        listing?.Feature(now, domainEvent.GrantedBy);
    }

    public async Task Handle(
        DomainEventNotification<ListingPromotionEndedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        if (!GrantsPlacement(domainEvent.Kind))
        {
            return;
        }

        var now = _clock.UtcNow;

        var stillRunning = await _promotions.GetActiveForListingAsync(
            domainEvent.ListingId, now, cancellationToken);

        if (stillRunning.Any(promotion =>
                promotion.Id != domainEvent.PromotionId && promotion.GrantsFeaturedPlacement))
        {
            return;
        }

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        listing?.Unfeature(now, domainEvent.EndedBy);
    }

    /// <summary>
    /// Whether a promotion kind grants the featured flag. A bump republishes and a category
    /// highlight pins within one page; neither is featured placement.
    /// </summary>
    private static bool GrantsPlacement(PromotionKind kind) =>
        kind is PromotionKind.Featured or PromotionKind.Spotlight;
}
