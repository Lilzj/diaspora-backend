using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Diaspora.SharedKernel.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Keeps the search index in step with a listing's visibility.
/// <para>
/// <b>One class for five events, because they are one job.</b> A status change, a moderation
/// decision, a deletion, a restoration and a content edit each change whether or in what form a
/// listing should appear in results, and the decision is always the same: project it, and index the
/// document or remove the entry according to whether a document came back.
/// </para>
/// <para>
/// <b>Removal failures are logged louder than indexing failures</b>, and the asymmetry is the point.
/// A listing that is live but not yet indexed is a listing somebody has to search harder for; a
/// listing that has sold, been hidden for fraud or been deleted and is <em>still</em> indexed is the
/// platform continuing to advertise something it decided to stop advertising.
/// </para>
/// <para>
/// Implements MediatR's notification handler directly rather than deriving
/// <c>DomainEventHandler&lt;T&gt;</c>: the base class can only unwrap one event type, and this
/// handler answers to four. The wrapper is unwrapped by hand in each method, which is the whole
/// difference.
/// </para>
/// </summary>
public sealed class ListingLifecycleEventHandler :
    INotificationHandler<DomainEventNotification<ListingStatusChangedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ListingModerationChangedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ListingDeletedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ListingRestoredDomainEvent>>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IMarketplaceSearchEngine _search;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingCategoryRepository _categories;
    private readonly ILogger<ListingLifecycleEventHandler> _logger;

    public ListingLifecycleEventHandler(
        MarketplaceIntegrationEventRelay relay,
        IMarketplaceSearchEngine search,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingCategoryRepository categories,
        ILogger<ListingLifecycleEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _listings = listings;
        _sellers = sellers;
        _categories = categories;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<ListingStatusChangedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        await _relay.PublishAsync(domainEvent, cancellationToken);
        await ReindexAsync(domainEvent.ListingId, cancellationToken);

        // A listing that has left the market takes its category count with it. Publication added
        // it; the first departure removes it, and a republication adds it back.
        if (!Domain.Policies.ListingStatusTransitions.IsPubliclyVisible(domainEvent.CurrentStatus)
            && Domain.Policies.ListingStatusTransitions.IsPubliclyVisible(
                domainEvent.PreviousStatus))
        {
            await AdjustCategoryCountAsync(domainEvent.ListingId, -1, cancellationToken);
        }
    }

    public async Task Handle(
        DomainEventNotification<ListingModerationChangedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await ReindexAsync(notification.DomainEvent.ListingId, cancellationToken);
    }

    public async Task Handle(
        DomainEventNotification<ListingDeletedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        await _relay.PublishAsync(domainEvent, cancellationToken);
        await RemoveFromIndexAsync(domainEvent.ListingId.Value, cancellationToken);
        await AdjustCategoryCountAsync(domainEvent.ListingId, -1, cancellationToken);

        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);
        seller?.ApplyPublishedDelta(-1);
    }

    public async Task Handle(
        DomainEventNotification<ListingRestoredDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        await _relay.PublishAsync(domainEvent, cancellationToken);

        // Deliberately no re-index and no counter change. A restored listing comes back as a draft;
        // putting it into discovery here would advertise something its seller has not re-published,
        // and the publish handler does both when they do.
    }

    /// <summary>
    /// Projects the listing and either indexes the document or removes the entry, according to
    /// whether the projection produced one.
    /// </summary>
    private async Task ReindexAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await _listings.GetAsync(listingId, cancellationToken);

            if (listing is null)
            {
                await RemoveFromIndexAsync(listingId.Value, cancellationToken);
                return;
            }

            var seller = await _sellers.GetAsync(listing.SellerId, cancellationToken);

            if (seller is null)
            {
                await RemoveFromIndexAsync(listingId.Value, cancellationToken);
                return;
            }

            var category = await _categories.GetByKeyAsync(
                listing.CategoryKey, cancellationToken);

            var document = ListingSearchProjectionService.Project(listing, seller, category?.Path);

            if (document is null)
            {
                await RemoveFromIndexAsync(listingId.Value, cancellationToken);
                return;
            }

            await _search.IndexAsync(document, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An index failure must not roll back the write that caused it.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Re-indexing listing {ListingId} failed. The next reconciliation pass will correct "
                + "it.",
                listingId.Value);
        }
#pragma warning restore CA1031
    }

    private async Task RemoveFromIndexAsync(Guid listingId, CancellationToken cancellationToken)
    {
        try
        {
            await _search.RemoveAsync(listingId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Logged at error, deliberately. See the class summary.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Removing listing {ListingId} from the search index failed. It may remain "
                + "discoverable until the next reconciliation pass.",
                listingId);
        }
#pragma warning restore CA1031
    }

    private async Task AdjustCategoryCountAsync(
        MarketplaceListingId listingId, int delta, CancellationToken cancellationToken)
    {
        var listing = await _listings.GetAsync(listingId, cancellationToken);

        if (listing is null)
        {
            return;
        }

        var category = await _categories.GetByKeyAsync(listing.CategoryKey, cancellationToken);

        category?.ApplyListingDelta(delta);
    }
}
