using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Extensibility.Ranking;

namespace Diaspora.Modules.Housing.Infrastructure.Ranking;

/// <summary>
/// Ready to move into soonest.
/// <para>
/// What somebody with a flight booked sorts by, and the ordering the module exists to serve: a
/// property available today beats a better one available in June when you land next week.
/// </para>
/// <para>
/// A property with no move-in date means "now" and sorts first, not last. Treating an absent date
/// as far-future would bury exactly the properties somebody in a hurry most wants.
/// </para>
/// </summary>
public sealed class SoonestRanking : IHousingRankingStrategy
{
    public string Key => HousingRankingKeys.Soonest;

    public IReadOnlyList<HousingListing> Rank(
        IReadOnlyList<HousingListing> listings, HousingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(listings);

        return
        [
            .. listings
                .OrderBy(listing => listing.Availability.AvailableFrom ?? DateTimeOffset.MinValue)
                .ThenByDescending(listing => listing.PublishedAt ?? listing.CreatedAt)
                .ThenBy(listing => listing.Slug.Value, StringComparer.Ordinal)
        ];
    }
}
