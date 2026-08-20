using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Diaspora.Modules.Marketplace.Observability;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// A listing became visible: publish the contract, index it, count it.
/// <para>
/// <b>Indexing is best-effort and deliberately does not fail the publication.</b> A listing that is
/// live but not yet indexed is reachable by every other route and will be picked up by the next
/// reconciliation pass; a publication rolled back because the index was briefly unreachable would
/// lose the seller's work.
/// </para>
/// <para>
/// The category counter is adjusted here rather than in the aggregate, because a category's listing
/// count is a fact about the catalogue and not about any one listing — and loading the category
/// inside the listing's own transaction would put a contended row in every publish.
/// </para>
/// </summary>
public sealed class ListingPublishedEventHandler
    : DomainEventHandler<ListingPublishedDomainEvent>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IMarketplaceSearchEngine _search;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingCategoryRepository _categories;
    private readonly IListingAnalyticsRepository _analytics;
    private readonly ILogger<ListingPublishedEventHandler> _logger;

    public ListingPublishedEventHandler(
        MarketplaceIntegrationEventRelay relay,
        IMarketplaceSearchEngine search,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingCategoryRepository categories,
        IListingAnalyticsRepository analytics,
        ILogger<ListingPublishedEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _listings = listings;
        _sellers = sellers;
        _categories = categories;
        _analytics = analytics;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        ListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        MarketplaceTelemetry.ListingsPublished.Add(
            1,
            new KeyValuePair<string, object?>("country", domainEvent.CountryCode),
            new KeyValuePair<string, object?>("category", domainEvent.CategoryKey));

        // Only the first publication counts. A seller who takes a listing down and puts it back up
        // has not published two things, and a count that said so would inflate every statistic
        // downstream of it.
        if (domainEvent.IsFirstPublication)
        {
            var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);
            seller?.ApplyPublishedDelta(1);

            var category = await _categories.GetByKeyAsync(
                domainEvent.CategoryKey, cancellationToken);

            category?.ApplyListingDelta(1);

            var analytics = await _analytics.GetForListingAsync(
                domainEvent.ListingId, cancellationToken);

            analytics?.RecordPublication(domainEvent.PublishedAt);
        }

        await IndexAsync(domainEvent, cancellationToken);
    }

    private async Task IndexAsync(
        ListingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
            var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);

            if (listing is null || seller is null)
            {
                return;
            }

            var category = await _categories.GetByKeyAsync(
                listing.CategoryKey, cancellationToken);

            var document = ListingSearchProjectionService.Project(listing, seller, category?.Path);

            if (document is not null)
            {
                await _search.IndexAsync(document, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An index failure must not roll back a publication.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Indexing listing {ListingId} failed. It is live and will be picked up by the next "
                + "sweep.",
                domainEvent.ListingId.Value);
        }
#pragma warning restore CA1031
    }
}
