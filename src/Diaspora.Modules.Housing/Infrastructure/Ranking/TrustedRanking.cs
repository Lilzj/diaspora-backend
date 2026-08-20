using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Extensibility.Ranking;

namespace Diaspora.Modules.Housing.Infrastructure.Ranking;

/// <summary>
/// Verified properties first, then newest.
/// <para>
/// The ranking a member choosing a home from another country wants. They cannot visit, cannot meet
/// the landlord and have no way to tell a real advert from a copied one - so a property somebody
/// has actually checked is worth more to them than a fresher one nobody has.
/// </para>
/// </summary>
public sealed class TrustedRanking : IHousingRankingStrategy
{
    public string Key => HousingRankingKeys.Trusted;

    public IReadOnlyList<HousingListing> Rank(
        IReadOnlyList<HousingListing> listings, HousingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(listings);

        return
        [
            .. listings
                .OrderByDescending(listing => listing.TrustMark.IsVerified)
                .ThenByDescending(listing => listing.PublishedAt ?? listing.CreatedAt)
                .ThenBy(listing => listing.Slug.Value, StringComparer.Ordinal)
        ];
    }
}
