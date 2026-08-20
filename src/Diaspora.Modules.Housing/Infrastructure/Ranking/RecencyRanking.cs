using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Extensibility.Ranking;

namespace Diaspora.Modules.Housing.Infrastructure.Ranking;

/// <summary>
/// Newest first. The default, and the one members implicitly expect on a housing search.
/// <para>
/// Housing turns over quickly and the good properties go within hours, so a listing published this
/// morning is genuinely more useful than a better one published three weeks ago that has almost
/// certainly gone.
/// </para>
/// </summary>
public sealed class RecencyRanking : IHousingRankingStrategy
{
    public string Key => HousingRankingKeys.Recency;

    public IReadOnlyList<HousingListing> Rank(
        IReadOnlyList<HousingListing> listings, HousingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(listings);

        // Ordered by slug as a tie-break: an unstable sort makes a cursor page repeat rows.
        return
        [
            .. listings
                .OrderByDescending(listing => listing.PublishedAt ?? listing.CreatedAt)
                .ThenBy(listing => listing.Slug.Value, StringComparer.Ordinal)
        ];
    }
}
