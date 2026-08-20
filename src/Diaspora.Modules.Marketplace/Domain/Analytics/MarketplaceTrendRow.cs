namespace Diaspora.Modules.Marketplace.Domain.Analytics;

/// <summary>
/// One row of a trend aggregate — a category or a country, with what happened in it.
/// <para>
/// A flat record rather than a projection of the rollup aggregate, because it is the shape of a
/// <c>GROUP BY</c> and nothing else. Making it an entity would invite somebody to try to save one.
/// </para>
/// </summary>
public sealed record MarketplaceTrendRow
{
    /// <summary>The category key or the country code, depending on which trend was asked for.</summary>
    public required string Key { get; init; }

    public required int Listings { get; init; }

    public required long Views { get; init; }

    public required long Offers { get; init; }

    public required long Reservations { get; init; }

    public required long Sales { get; init; }

    /// <summary>Sales over views. Null when nobody looked, which is not a zero-percent conversion.</summary>
    public double? ConversionRate => Views == 0 ? null : (double)Sales / Views;
}
