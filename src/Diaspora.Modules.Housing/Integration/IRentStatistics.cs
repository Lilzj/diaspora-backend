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
