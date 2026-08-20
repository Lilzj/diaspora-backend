namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// The ranking keys B7 ships.
/// <para>
/// Constants rather than an enum, because the set is open: a search module registering
/// <c>relevance</c> and a recommendation module registering <c>personalised</c> must not need an
/// edit here to do it. These are the ones that exist on a deployment with nothing else
/// installed.
/// </para>
/// </summary>
public static class RankingKeys
{
    /// <summary>The client's "Recommended". Balanced, explicable, and deliberately not clever.</summary>
    public const string Balanced = "balanced";

    /// <summary>Nearest first. The client's "Distance".</summary>
    public const string Proximity = "proximity";

    /// <summary>Best rated first, weighted by how many reviews there are. The client's "Rating".</summary>
    public const string Rating = "rating";

    /// <summary>Most recently published first.</summary>
    public const string Recency = "recency";
}
