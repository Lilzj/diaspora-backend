using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Analytics;

/// <summary>How the application layer reaches the analytics rollups.</summary>
public interface IListingAnalyticsRepository
{
    /// <summary>The rollup with its daily series, tracked. For a handler recording a movement.</summary>
    Task<ListingAnalytics?> GetForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The rollup with a bounded daily series, untracked. For the dashboard, which renders a
    /// window rather than the whole life of the listing.
    /// </summary>
    Task<ListingAnalytics?> GetForReadAsync(
        MarketplaceListingId listingId,
        DateOnly? from,
        CancellationToken cancellationToken = default);

    /// <summary>Every rollup of one seller. What the seller performance dashboard aggregates.</summary>
    Task<IReadOnlyList<ListingAnalytics>> GetForSellerAsync(
        SellerProfileId sellerId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Category trends: totals grouped by category key over a window.
    /// <para>
    /// Answered by the database rather than by loading rollups, because "what is moving in this
    /// country this month" over a marketplace of any size is a group-by and not a projection.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<MarketplaceTrendRow>> GetCategoryTrendsAsync(
        string? countryCode,
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Geographic trends: the same totals grouped by country.</summary>
    Task<IReadOnlyList<MarketplaceTrendRow>> GetCountryTrendsAsync(
        string? categoryKey,
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(ListingAnalytics analytics, CancellationToken cancellationToken = default);
}
