using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Integration;

namespace Diaspora.Modules.Housing.Infrastructure.Statistics;

/// <summary>
/// Medians of the monthly asking rents of live listings, read through the repository's one
/// definition of "a property that is here".
/// </summary>
public sealed class HousingRentStatistics : IRentStatistics
{
    /// <summary>
    /// The most rows one comparison reads. A country's live rentals, newest first; far past what
    /// the platform holds, and a bound on what a single request can load.
    /// </summary>
    public const int MaximumListingsRead = 20_000;

    private readonly IHousingListingRepository _listings;

    public HousingRentStatistics(IHousingListingRepository listings) => _listings = listings;

    public async Task<RentComparison> CompareMonthlyRentAsync(
        string countryCode, string areaPath, CancellationToken cancellationToken = default)
    {
        var prices = await _listings.GetRentPricesAsync(countryCode, MaximumListingsRead, cancellationToken);

        // The country's own currency, by what its listings use. Ties go to the alphabetically first
        // code so the same data always compares in the same currency.
        var currency = prices
            .GroupBy(price => price.CurrencyCode.Trim().ToUpperInvariant(), StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (currency is null)
        {
            return new RentComparison
            {
                AreaSampleSize = 0,
                CountrySampleSize = 0,
                OtherCurrencyListings = 0
            };
        }

        var comparable = prices
            .Where(price => string.Equals(price.CurrencyCode.Trim(), currency, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var country = comparable.Select(Monthly).ToList();

        var area = comparable
            .Where(price => IsWithin(price.AreaPath, areaPath))
            .Select(Monthly)
            .ToList();

        return new RentComparison
        {
            Currency = currency,
            AreaSampleSize = area.Count,
            AreaMedianMonthlyRent = Median(area),
            CountrySampleSize = country.Count,
            CountryMedianMonthlyRent = Median(country),
            OtherCurrencyListings = prices.Count - comparable.Count,
            NewestListingPublishedAt = comparable.Max(price => price.PublishedAt)
        };
    }

    /// <summary>A rent expressed per month. Weeks are 52 to the year, so a month is 52/12 of them.</summary>
    public static decimal Monthly(HousingRentPrice price)
    {
        ArgumentNullException.ThrowIfNull(price);

        return price.Period switch
        {
            RentPeriod.PerWeek => price.Amount * 52m / 12m,
            RentPeriod.PerQuarter => price.Amount / 3m,
            RentPeriod.PerYear => price.Amount / 12m,
            _ => price.Amount
        };
    }

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

    /// <summary>
    /// Whether a home filed at <paramref name="path"/> is in the area at <paramref name="areaPath"/>:
    /// the area itself or anywhere beneath it. A prefix alone would put "/mu/quatre-bornes-west" inside
    /// "/mu/quatre-bornes".
    /// </summary>
    private static bool IsWithin(string? path, string areaPath) =>
        path is not null
        && (string.Equals(path, areaPath, StringComparison.Ordinal)
            || path.StartsWith(areaPath.TrimEnd('/') + "/", StringComparison.Ordinal));
}
