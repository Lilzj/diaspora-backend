using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// A listing's content changed: publish the contract and refresh the index entry.
/// <para>
/// <b>The price travels on the contract because the price-watch fan-out cannot work without it.</b>
/// The aggregate already holds the new figure by the time this runs, so the old one is read from
/// the listing's own state only where the handler that raised the event passed it forward. Where it
/// did not — a title or a tag edit — the contract carries no price and consumers ignore that half.
/// </para>
/// </summary>
public sealed class ListingUpdatedEventHandler : DomainEventHandler<ListingUpdatedDomainEvent>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IMarketplaceSearchEngine _search;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingCategoryRepository _categories;
    private readonly ILogger<ListingUpdatedEventHandler> _logger;

    public ListingUpdatedEventHandler(
        MarketplaceIntegrationEventRelay relay,
        IMarketplaceSearchEngine search,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingCategoryRepository categories,
        ILogger<ListingUpdatedEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _listings = listings;
        _sellers = sellers;
        _categories = categories;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        ListingUpdatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        var carriesPrice = domainEvent.ChangedFields.Contains("price", StringComparer.Ordinal);

        await _relay.PublishAsync(
            domainEvent,
            previousPrice: null,
            currentPrice: carriesPrice ? listing?.Price.Amount : null,
            currencyCode: carriesPrice ? listing?.Price.CurrencyCode : null,
            cancellationToken);

        if (listing is null)
        {
            return;
        }

        try
        {
            var seller = await _sellers.GetAsync(listing.SellerId, cancellationToken);

            if (seller is null)
            {
                return;
            }

            var category = await _categories.GetByKeyAsync(
                listing.CategoryKey, cancellationToken);

            var document = ListingSearchProjectionService.Project(listing, seller, category?.Path);

            if (document is null)
            {
                await _search.RemoveAsync(listing.Id.Value, cancellationToken);
                return;
            }

            await _search.IndexAsync(document, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An index failure must not roll back the seller's edit.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Re-indexing listing {ListingId} after an edit failed.",
                domainEvent.ListingId.Value);
        }
#pragma warning restore CA1031
    }
}
