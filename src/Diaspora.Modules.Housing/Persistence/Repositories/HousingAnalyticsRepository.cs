using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for the measured record.
/// <para>
/// <b>The daily breakdown is included only where it is rendered.</b> A landlord's sparkline needs
/// it; a neighbourhood market statistic needs the running totals and nothing else, and including
/// ninety rows per property across a whole neighbourhood would be several thousand rows to compute
/// one median.
/// </para>
/// </summary>
public sealed class HousingAnalyticsRepository : IHousingAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public HousingAnalyticsRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<HousingAnalytics> Analytics => _dbContext.Set<HousingAnalytics>();

    public async Task<HousingAnalytics?> GetByListingAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Analytics
            .Include(analytics => analytics.Daily)
            .FirstOrDefaultAsync(
                analytics => analytics.ListingId == listingId, cancellationToken);
    }

    public async Task<HousingAnalytics?> GetByListingForReadAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Analytics.AsNoTracking()
            .Include(analytics => analytics.Daily)
            .FirstOrDefaultAsync(
                analytics => analytics.ListingId == listingId, cancellationToken);
    }

    /// <summary>
    /// Batched, without the daily breakdown. A landlord dashboard covering twenty properties wants
    /// twenty headline figures, not eighteen hundred daily rows.
    /// </summary>
    public async Task<IReadOnlyDictionary<HousingListingId, HousingAnalytics>> GetManyAsync(
        IReadOnlyCollection<HousingListingId> listingIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingIds);

        if (listingIds.Count == 0)
        {
            return new Dictionary<HousingListingId, HousingAnalytics>();
        }

        var keys = listingIds.Distinct().ToList();

        var rows = await Analytics.AsNoTracking()
            .Where(analytics => keys.Contains(analytics.ListingId))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(analytics => analytics.ListingId);
    }

    /// <summary>
    /// Projects the market rows a neighbourhood statistic is computed from.
    /// <para>
    /// Returns rows rather than an average deliberately — the caller must group by currency and by
    /// rent period before averaging anything, because nothing in this module converts either. An
    /// average that quietly mixed a nightly hostel rate into a monthly rent would be the most
    /// misleading number the platform could publish about a place.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<HousingMarketRow>> GetMarketRowsAsync(
        Guid? areaId,
        string? countryCode,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var query = Analytics.AsNoTracking()
            .Where(analytics => analytics.CreatedAt >= since);

        if (areaId is { } area && area != Guid.Empty)
        {
            query = query.Where(analytics => analytics.NeighborhoodAreaId == area);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            query = query.Where(analytics => analytics.CountryCode == country);
        }

        // Projected in the database: the caller wants six columns and the aggregate has forty.
        return await query
            .Select(analytics => new HousingMarketRow
            {
                NeighborhoodAreaId = analytics.NeighborhoodAreaId,
                CountryCode = analytics.CountryCode,
                AccommodationTypeKey = analytics.AccommodationTypeKey,
                Amount = analytics.LetAtAmount,
                CurrencyCode = analytics.LetAtCurrency,
                Period = analytics.LetAtPeriod,
                TimeToLet = analytics.TimeToLet,
                HasLet = analytics.FirstOccupiedAt != null,
                RecordedAt = analytics.FirstOccupiedAt ?? analytics.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountViewsForLandlordAsync(
        LandlordProfileId landlordId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlordId);

        var from = DateOnly.FromDateTime(since.UtcDateTime);

        return await Analytics
            .AsNoTracking()
            .Where(analytics => analytics.LandlordId == landlordId)
            .SelectMany(analytics => analytics.Daily)
            .Where(metric => metric.Day >= from)
            .SumAsync(metric => (long)metric.Views, cancellationToken);
    }

    public async Task AddAsync(
        HousingAnalytics analytics, CancellationToken cancellationToken = default) =>
        await Analytics.AddAsync(analytics, cancellationToken);
}
