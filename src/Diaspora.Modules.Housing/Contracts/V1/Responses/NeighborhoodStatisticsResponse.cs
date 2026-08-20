namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// What the platform can say about a neighbourhood in numbers.
/// <para>
/// <b>Rents are reported per currency and per rent period, never averaged across either.</b> A
/// figure that mixed a nightly hostel rate into a monthly flat average would be wrong in a way
/// nobody reading it could see, and it would be the most misleading number this platform could
/// publish about a place.
/// </para>
/// </summary>
public sealed record NeighborhoodStatisticsResponse
{
    /// <summary>Live properties here.</summary>
    public required int ListingCount { get; init; }

    /// <summary>Published community insights.</summary>
    public required int InsightCount { get; init; }

    /// <summary>Rent statistics, one row per currency and period pair.</summary>
    public IReadOnlyList<HousingMarketStatisticResponse> Market { get; init; } = [];

    /// <summary>
    /// Median days from publication to somebody moving in. Null until enough properties here have
    /// let for a median to mean anything.
    /// </summary>
    public double? MedianDaysToLet { get; init; }

    /// <summary>
    /// Counts contributed by other verticals, through B4 registry - jobs, marketplace, providers,
    /// community. Housing reads these and owns none of them.
    /// </summary>
    public IReadOnlyDictionary<string, int> NearbyResourceCounts { get; init; } =
        new Dictionary<string, int>();
}
