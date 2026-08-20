using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Diaspora.Modules.Marketplace.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Propagates a change in a seller to every listing that carries their badge.
/// <para>
/// <b>A suspension that left a hundred listings live would be a suspension in name only.</b> The
/// seller aggregate deliberately does not cascade — loading four hundred listings inside one command
/// would make suspending a shop a minute-long transaction — so the cascade happens here, bounded,
/// after the seller's own write has committed.
/// </para>
/// <para>
/// <b>Nothing about the listings themselves changes.</b> They come out of the index and go back in
/// when the suspension lifts, because the projection refuses to produce a document for a seller who
/// cannot trade. Their own status, stock and history are untouched, which is what makes reinstating
/// a seller restore exactly what was there.
/// </para>
/// <para>
/// <b>A verification change re-indexes for a different reason.</b> The badge on every card is
/// computed from the seller, so a revocation has to reach every document — and a consumer holding a
/// stale "Verified Business" is the platform vouching for exactly the wrong party.
/// </para>
/// </summary>
public sealed class SellerEventHandler :
    INotificationHandler<DomainEventNotification<SellerVerificationChangedDomainEvent>>,
    INotificationHandler<DomainEventNotification<SellerStatusChangedDomainEvent>>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IMarketplaceSearchEngine _search;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingCategoryRepository _categories;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly ILogger<SellerEventHandler> _logger;

    public SellerEventHandler(
        MarketplaceIntegrationEventRelay relay,
        IMarketplaceSearchEngine search,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingCategoryRepository categories,
        IOptions<MarketplaceOptions> options,
        ILogger<SellerEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _listings = listings;
        _sellers = sellers;
        _categories = categories;
        _options = options;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<SellerVerificationChangedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var domainEvent = notification.DomainEvent;

        await _relay.PublishAsync(domainEvent, cancellationToken);
        await ReindexSellerListingsAsync(domainEvent.SellerId, cancellationToken);
    }

    public async Task Handle(
        DomainEventNotification<SellerStatusChangedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await ReindexSellerListingsAsync(notification.DomainEvent.SellerId, cancellationToken);
    }

    /// <summary>
    /// Re-projects every live listing of one seller.
    /// <para>
    /// <b>Bounded by the sweep batch size, deliberately.</b> A shop with ten thousand listings
    /// cannot be re-indexed inside one event handler; what this does is correct the visible ones
    /// immediately and leave the tail to the reconciliation pass, which is a slower fix rather than
    /// a missing one.
    /// </para>
    /// </summary>
    private async Task ReindexSellerListingsAsync(
        Domain.Identifiers.SellerProfileId sellerId, CancellationToken cancellationToken)
    {
        try
        {
            var seller = await _sellers.GetAsync(sellerId, cancellationToken);

            if (seller is null)
            {
                return;
            }

            var listings = await _listings.GetActiveBySellerAsync(
                sellerId, _options.Value.SweepBatchSize, cancellationToken);

            foreach (var listing in listings)
            {
                var category = await _categories.GetByKeyAsync(
                    listing.CategoryKey, cancellationToken);

                var document = ListingSearchProjectionService.Project(
                    listing, seller, category?.Path);

                if (document is null)
                {
                    await _search.RemoveAsync(listing.Id.Value, cancellationToken);
                    continue;
                }

                await _search.IndexAsync(document, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An index failure must not roll back a moderator's decision.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Re-indexing the listings of seller {SellerId} failed. Their badge or visibility "
                + "may be stale in search until the next reconciliation pass.",
                sellerId.Value);
        }
#pragma warning restore CA1031
    }
}
