using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using MediatR;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Keeps a listing's counters, its media claims and its category counts honest.
/// <para>
/// <b>Four small facts that all move numbers on rows other than the one that raised them.</b> A save
/// belongs to a member and adjusts the listing; a media attach belongs to the listing and has to
/// reach Media; a category change adjusts two catalogue rows. Each is a cross-row write that would
/// be wrong inside the aggregate that caused it.
/// </para>
/// <para>
/// <b>None of them bumps a revision.</b> A stranger saving a listing must never make the seller's
/// in-flight edit fail with a conflict they cannot explain — which is exactly why these are counter
/// mutations on the aggregate rather than ordinary ones.
/// </para>
/// </summary>
public sealed class ListingEngagementEventHandler :
    INotificationHandler<DomainEventNotification<ListingSavedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ListingMediaAttachedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ListingCategoryChangedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ListingFeaturedDomainEvent>>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IListingCategoryRepository _categories;
    private readonly IListingAnalyticsRepository _analytics;
    private readonly MarketplaceMediaClaimer _media;
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IClock _clock;

    public ListingEngagementEventHandler(
        IMarketplaceListingRepository listings,
        IListingCategoryRepository categories,
        IListingAnalyticsRepository analytics,
        MarketplaceMediaClaimer media,
        MarketplaceIntegrationEventRelay relay,
        IClock clock)
    {
        _listings = listings;
        _categories = categories;
        _analytics = analytics;
        _media = media;
        _relay = relay;
        _clock = clock;
    }

    /// <summary>
    /// A member saved or unsaved: adjust the listing's count and the day's bucket.
    /// <para>
    /// Deliberately publishes nothing. Who is watching what is the most behaviourally revealing
    /// thing this module knows about a member, and no other module needs it.
    /// </para>
    /// </summary>
    public async Task Handle(
        DomainEventNotification<ListingSavedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;
        var delta = domainEvent.IsSaved ? 1 : -1;

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        listing?.ApplySaveDelta(delta);

        var analytics = await _analytics.GetForListingAsync(
            domainEvent.ListingId, cancellationToken);

        analytics?.RecordSaveDelta(_clock.UtcNow, delta);
    }

    /// <summary>
    /// A photograph was attached: tell Media it is claimed, and publish the reference.
    /// <para>
    /// <b>Claiming is not optional.</b> Media sweeps assets nothing ever attached, so a listing
    /// whose photographs were never claimed loses them a day later — silently, because every upload
    /// and every attach succeeded.
    /// </para>
    /// </summary>
    public async Task Handle(
        DomainEventNotification<ListingMediaAttachedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        await _media.ClaimAsync(
            domainEvent.ListingId.Value,
            domainEvent.MediaId,
            domainEvent.Role.ToString(),
            position: 0,
            cancellationToken);

        await _relay.PublishAsync(domainEvent, cancellationToken);
    }

    /// <summary>
    /// A listing moved category: one count down, one up. Only for listings that are actually
    /// visible — a draft was never counted, so moving it must not decrement anything.
    /// </summary>
    public async Task Handle(
        DomainEventNotification<ListingCategoryChangedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        if (!domainEvent.IsDiscoverable)
        {
            return;
        }

        var previous = await _categories.GetByKeyAsync(
            domainEvent.PreviousCategoryKey, cancellationToken);

        previous?.ApplyListingDelta(-1);

        var current = await _categories.GetByKeyAsync(
            domainEvent.CurrentCategoryKey, cancellationToken);

        current?.ApplyListingDelta(1);

        var analytics = await _analytics.GetForListingAsync(
            domainEvent.ListingId, cancellationToken);

        // The rollup's denormalised dimension follows the listing, or every trend from this point
        // on would attribute the listing's whole history to the category it left.
        analytics?.Reclassify(domainEvent.CurrentCategoryKey, countryCode: null);
    }

    public Task Handle(
        DomainEventNotification<ListingFeaturedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return _relay.PublishAsync(
            notification.DomainEvent, cancellationToken: cancellationToken);
    }
}
