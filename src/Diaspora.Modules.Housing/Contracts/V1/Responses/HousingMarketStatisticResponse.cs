namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// Rent statistics for one currency and one rent period.
/// <para>
/// Scoped to a pair because there is no single answer across them. See
/// <see cref="NeighborhoodStatisticsResponse"/>.
/// </para>
/// </summary>
public sealed record HousingMarketStatisticResponse
{
    public required string CurrencyCode { get; init; }

    public required string Period { get; init; }

    /// <summary>The accommodation type, or null for all types together.</summary>
    public string? AccommodationTypeKey { get; init; }

    /// <summary>How many lets this is computed from. A client hides a statistic from a small sample.</summary>
    public required int SampleSize { get; init; }

    /// <summary>The median rather than the mean: one luxury let should not move the headline.</summary>
    public decimal? MedianRent { get; init; }

    public decimal? LowestRent { get; init; }

    public decimal? HighestRent { get; init; }
}
