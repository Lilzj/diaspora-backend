using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// EF Core repository for the analytics rollups.
/// <para>
/// <b>The two trend reads are answered by the database, not by loading rollups.</b> "What is moving
/// in this country this month" over a marketplace of any size is a <c>GROUP BY</c>, and a version
/// that materialised every rollup to sum them in memory would work in a test and time out in
/// production.
/// </para>
/// </summary>
public sealed class ListingAnalyticsRepository : IListingAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ListingAnalyticsRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ListingAnalytics> Analytics => _dbContext.Set<ListingAnalytics>();

    public async Task<ListingAnalytics?> GetForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Analytics.Include(analytics => analytics.Daily)
            .FirstOrDefaultAsync(analytics => analytics.ListingId == listingId, cancellationToken);
    }

    /// <summary>
    /// The rollup with a bounded daily series.
    /// <para>
    /// <b>EF cannot filter an owned collection inside an Include</b>, so the whole series comes back
    /// and the window is applied after materialisation. That is acceptable because the grain is
    /// daily: a listing that has been up for three years has about a thousand rows, which is a page
    /// of data rather than a table scan. A finer grain would have made this the wrong shape.
    /// </para>
    /// </summary>
    public async Task<ListingAnalytics?> GetForReadAsync(
        MarketplaceListingId listingId,
        DateOnly? from,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Analytics.AsNoTracking().Include(analytics => analytics.Daily)
            .FirstOrDefaultAsync(analytics => analytics.ListingId == listingId, cancellationToken);
    }

    public async Task<IReadOnlyList<ListingAnalytics>> GetForSellerAsync(
        SellerProfileId sellerId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sellerId);

        // Without the daily series: a seller dashboard aggregates lifetime totals, and pulling a
        // thousand daily rows per listing to sum seven numbers would be the same mistake the trend
        // reads avoid.
        return await Analytics.AsNoTracking()
            .Where(analytics => analytics.SellerId == sellerId)
            .OrderByDescending(analytics => analytics.TotalViews)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketplaceTrendRow>> GetCategoryTrendsAsync(
        string? countryCode,
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDailyQuery(countryCode, categoryKey: null, from, to);

        var rows = await query
            .GroupBy(row => row.CategoryKey)
            .Select(group => new MarketplaceTrendRow
            {
                Key = group.Key,
                Listings = group.Select(row => row.ListingId).Distinct().Count(),
                Views = group.Sum(row => (long)row.Views),
                Offers = group.Sum(row => (long)row.Offers),
                Reservations = group.Sum(row => (long)row.Reservations),
                Sales = group.Sum(row => (long)row.Sales)
            })
            .OrderByDescending(row => row.Views)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<MarketplaceTrendRow>> GetCountryTrendsAsync(
        string? categoryKey,
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDailyQuery(countryCode: null, categoryKey, from, to);

        var rows = await query
            .GroupBy(row => row.CountryCode)
            .Select(group => new MarketplaceTrendRow
            {
                Key = group.Key,
                Listings = group.Select(row => row.ListingId).Distinct().Count(),
                Views = group.Sum(row => (long)row.Views),
                Offers = group.Sum(row => (long)row.Offers),
                Reservations = group.Sum(row => (long)row.Reservations),
                Sales = group.Sum(row => (long)row.Sales)
            })
            .OrderByDescending(row => row.Sales)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task AddAsync(
        ListingAnalytics analytics, CancellationToken cancellationToken = default) =>
        await Analytics.AddAsync(analytics, cancellationToken);

    /// <summary>
    /// The daily buckets in a window, joined to their rollup's dimensions.
    /// <para>
    /// The join is what lets the trend group by category or country without those columns living on
    /// every daily row — the dimensions belong to the listing, and duplicating them per day would
    /// be a thousand copies of a string that never changes.
    /// </para>
    /// </summary>
    private IQueryable<DailyTrendProjection> BuildDailyQuery(
        string? countryCode, string? categoryKey, DateOnly from, DateOnly to)
    {
        var rollups = Analytics.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            rollups = rollups.Where(analytics => analytics.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(categoryKey))
        {
            var category = categoryKey.Trim().ToLowerInvariant();
            rollups = rollups.Where(analytics => analytics.CategoryKey == category);
        }

        return rollups.SelectMany(
            analytics => analytics.Daily.Where(day => day.Day >= from && day.Day <= to),
            (analytics, day) => new DailyTrendProjection
            {
                ListingId = analytics.ListingId,
                CategoryKey = analytics.CategoryKey,
                CountryCode = analytics.CountryCode,
                Views = day.Views,
                Offers = day.Offers,
                Reservations = day.Reservations,
                Sales = day.Sales
            });
    }

    /// <summary>
    /// The flattened row a trend groups over. Private, because it is the shape of one query and
    /// nothing else — making it public would invite somebody to try to save one.
    /// </summary>
    private sealed record DailyTrendProjection
    {
        public required MarketplaceListingId ListingId { get; init; }

        public required string CategoryKey { get; init; }

        public required string CountryCode { get; init; }

        public required int Views { get; init; }

        public required int Offers { get; init; }

        public required int Reservations { get; init; }

        public required int Sales { get; init; }
    }
}
