using Diaspora.Modules.Housing.Domain.Listings;

namespace Diaspora.Modules.Housing.Extensibility.Ranking;

/// <summary>
/// Implemented to order a page of results by something the database cannot express.
/// <para>
/// <b>Ranking is a seam because "the best match" is a product judgement that will change, and
/// changing it must not mean changing a repository.</b> The shipped strategies order by recency,
/// by trust and by a balance of the two; a deployment that wants to lift properties whose landlord
/// answers viewing requests promptly ships a class, and the strategy appears in the picker.
/// </para>
/// <para>
/// <b>It ranks a page, not a table.</b> The repository has already narrowed and paged; a strategy
/// that needed the whole result set to rank would be a strategy that could not be applied to a
/// cursor page at all. That constraint is deliberate — it keeps ranking cheap and keeps paging
/// correct.
/// </para>
/// </summary>
public interface IHousingRankingStrategy
{
    /// <summary>The key a caller selects this strategy by. Unique across the platform.</summary>
    string Key { get; }

    /// <summary>Orders the page. Must be stable: an unstable sort makes paging repeat rows.</summary>
    IReadOnlyList<HousingListing> Rank(
        IReadOnlyList<HousingListing> listings, HousingRankingContext context);
}
