namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>
/// The ranking strategies B6 ships.
/// <para>
/// Open, like the feed keys: a deployment adds a ranking by registering a strategy with a new
/// key. The brief asks for chronological, trending, trust-weighted, location-weighted and a
/// future AI ranking, and names replaceability as the requirement — so the four that ship are
/// four registrations, not four branches of a method.
/// </para>
/// </summary>
public static class RankingKeys
{
    /// <summary>Newest first. The only ranking that is provably not manipulable.</summary>
    public const string Chronological = "chronological";

    /// <summary>Engagement velocity with time decay.</summary>
    public const string Trending = "trending";

    /// <summary>Weighted by what Trust says about the author.</summary>
    public const string TrustWeighted = "trust-weighted";

    /// <summary>Weighted by how close the content is to the reader.</summary>
    public const string LocationWeighted = "location-weighted";
}
