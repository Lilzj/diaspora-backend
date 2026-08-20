namespace Diaspora.Modules.Marketplace.Extensibility.Ranking;

/// <summary>
/// The keys the shipped ranking strategies claim.
/// <para>
/// Named constants rather than string literals at the call sites, so a request naming a strategy
/// and the strategy declaring itself cannot drift. A contributed strategy uses whatever key it
/// likes; these are simply the ones that arrive in the box.
/// </para>
/// </summary>
public static class ListingRankingKeys
{
    /// <summary>Newest first, with nothing else considered. The default browse.</summary>
    public const string Recency = "recency";

    /// <summary>Verified sellers and storefronts first, then recency.</summary>
    public const string Trusted = "trusted";

    /// <summary>Freshness, trust, engagement and completeness together. The default for search.</summary>
    public const string Balanced = "balanced";

    /// <summary>Closest first, degrading to recency where no point was supplied.</summary>
    public const string Proximity = "proximity";
}
