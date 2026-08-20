using Diaspora.Modules.Housing.Domain.Identifiers;

namespace Diaspora.Modules.Housing.Domain.Analytics;

/// <summary>How the module reaches its measured record.</summary>
public interface IHousingAnalyticsRepository
{
    Task<HousingAnalytics?> GetByListingAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default);

    Task<HousingAnalytics?> GetByListingForReadAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default);

    /// <summary>Batched, for a landlord dashboard covering twenty properties.</summary>
    Task<IReadOnlyDictionary<HousingListingId, HousingAnalytics>> GetManyAsync(
        IReadOnlyCollection<HousingListingId> listingIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The rent and time-to-let record for a neighbourhood, as rows to aggregate.
    /// <para>
    /// Deliberately returns rows rather than an average: the caller has to group by currency and
    /// by rent period before averaging anything, because nothing in this module converts either -
    /// and an average that quietly mixed a nightly hostel rate into a monthly rent would be the
    /// most misleading number the platform could publish about a neighbourhood.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<HousingMarketRow>> GetMarketRowsAsync(
        Guid? areaId,
        string? countryCode,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Views across all of a landlord's properties since a date, summed in the database.
    /// <para>
    /// Reads the daily rows rather than the running total, because "views in the last thirty days"
    /// and "views ever" are different questions and only the first tells a landlord whether the
    /// change they made last week did anything.
    /// </para>
    /// </summary>
    Task<long> CountViewsForLandlordAsync(
        LandlordProfileId landlordId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    Task AddAsync(HousingAnalytics analytics, CancellationToken cancellationToken = default);
}
