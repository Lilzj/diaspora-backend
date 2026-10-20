namespace Diaspora.Modules.Housing.Integration;

/// <summary>
/// What homes to rent are advertised for on Diaspora, in a place against its country.
/// <para>
/// <b>The only cost-of-living figure the platform can stand behind is its own.</b> The user decided
/// on 16 Sep 2026 that cost of living is computed from Diaspora's listings rather than bought in:
/// the median asking rent of the homes filed in a place, against the median of the country's. It is
/// housing only, and it is asking rents, not agreed ones — both of which a consumer must say.
/// </para>
/// <para>
/// <b>One currency, chosen per country, never converted.</b> The country's rents are compared in
/// the currency most of its live listings use, and a rent in any other currency is left out rather
/// than converted at a rate nobody on the page agreed to. Weekly, quarterly and yearly rents are
/// expressed per month; nightly and per-stay prices are holiday lets and are left out.
/// </para>
/// </summary>
public interface IRentStatistics
{
    /// <summary>
    /// The monthly asking rents filed within <paramref name="areaPath"/> against those of its whole
    /// country, in the country's most-listed currency. Never throws for an unknown place: a place
    /// with no listings is a comparison with a sample of zero.
    /// </summary>
    Task<RentComparison> CompareMonthlyRentAsync(
        string countryCode, string areaPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// The monthly asking rents of homes within <paramref name="radiusKm"/> of a point against those
    /// of the whole country, in the country's most-listed currency (14 Oct 2026, the area around a
    /// point). Each home is placed at the coordinate a stranger is shown, so a small circle cannot
    /// reveal more about where a home is than its card does; a home with no coordinate is in the
    /// country's sample and never in the circle's.
    /// </summary>
    Task<RentComparison> CompareMonthlyRentWithinAsync(
        string countryCode,
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every monthly asking rent in a country that a map can place, in the country's most-listed
    /// currency, with the country's own median beside them (21 Oct 2026, the rent heat layers).
    /// <para>
    /// <b>The same population and arithmetic as <see cref="CompareMonthlyRentAsync"/></b>: live,
    /// publicly visible homes with a disclosed rent; weekly, quarterly and yearly rents per month;
    /// nightly and per-stay prices left out; the currency chosen by what the country's live listings
    /// use, and other currencies counted and left out rather than converted. A caller buckets the
    /// points and takes medians with <see cref="RentArithmetic.Median"/>.
    /// </para>
    /// <para>
    /// Each point is the coordinate a stranger is shown, so a small cell cannot reveal more about
    /// where a home is than its card does. A home with no coordinate is in the country's sample and
    /// is not a point.
    /// </para>
    /// <para>
    /// When <paramref name="publishedFrom"/> or <paramref name="publishedTo"/> is given, both the
    /// points and the country's sample are the homes published inside that window, so an area and
    /// its baseline always describe the same period. The currency is still chosen from every live
    /// listing, so it does not change with the window.
    /// </para>
    /// </summary>
    Task<RentPointSample> SampleMonthlyRentsAsync(
        string countryCode,
        DateTimeOffset? publishedFrom = null,
        DateTimeOffset? publishedTo = null,
        CancellationToken cancellationToken = default);
}

/// <summary>One home's monthly asking rent, where a stranger is shown it.</summary>
public readonly record struct RentPoint(
    double Latitude, double Longitude, decimal MonthlyRent, DateTimeOffset? PublishedAt);

/// <summary>A country's placeable monthly rents, and the sample its baseline is taken over.</summary>
public sealed record RentPointSample
{
    /// <summary>The ISO currency every rent here is in. Null when the country has no disclosed rents.</summary>
    public string? Currency { get; init; }

    /// <summary>Homes in the country's sample (inside the window, when there is one), pinned or not.</summary>
    public required int CountrySampleSize { get; init; }

    public decimal? CountryMedianMonthlyRent { get; init; }

    /// <summary>Live listings in the country left out because their rent is in another currency.</summary>
    public required int OtherCurrencyListings { get; init; }

    /// <summary>The homes in the sample that have a coordinate.</summary>
    public required IReadOnlyList<RentPoint> Points { get; init; }

    /// <summary>True when the read stopped at its cap, so the sample is the newest homes rather than all.</summary>
    public bool IsTruncated { get; init; }
}

/// <summary>
/// The rent arithmetic every rent figure on the platform uses, in one place, so a map cell and a
/// neighbourhood page cannot take a median two ways.
/// </summary>
public static class RentArithmetic
{
    /// <summary>The middle value, or the mean of the middle two. Null for no values.</summary>
    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(value => value).ToList();
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2m;
    }
}

/// <summary>A place's median monthly asking rent against its country's, with the samples behind both.</summary>
public sealed record RentComparison
{
    /// <summary>The ISO currency both medians are in. Null when the country has no disclosed rents.</summary>
    public string? Currency { get; init; }

    /// <summary>Homes behind <see cref="AreaMedianMonthlyRent"/>.</summary>
    public required int AreaSampleSize { get; init; }

    public decimal? AreaMedianMonthlyRent { get; init; }

    /// <summary>Homes behind <see cref="CountryMedianMonthlyRent"/>, the place's included.</summary>
    public required int CountrySampleSize { get; init; }

    public decimal? CountryMedianMonthlyRent { get; init; }

    /// <summary>
    /// Live listings in the country left out because their rent is in another currency. Reported
    /// so a reader can see what the comparison did not count.
    /// </summary>
    public required int OtherCurrencyListings { get; init; }

    /// <summary>When the most recently published home in the country's sample went live.</summary>
    public DateTimeOffset? NewestListingPublishedAt { get; init; }
}
