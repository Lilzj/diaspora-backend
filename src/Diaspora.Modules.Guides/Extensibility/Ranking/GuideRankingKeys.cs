namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// The ranking keys B8 ships.
/// <para>
/// Constants rather than an enum, because the set is open: a search module registering
/// <c>relevance</c> must not need an edit here to do it. These are the ones that exist on a
/// deployment with nothing else installed, and they mirror the wire sort keys deliberately —
/// one vocabulary, lower-case, frozen.
/// </para>
/// </summary>
public static class GuideRankingKeys
{
    /// <summary>Most recently published first. The default, and the only ordering with no judgement in it.</summary>
    public const string Newest = "newest";

    /// <summary>Best helpfulness record first, weighted by how many readers voted.</summary>
    public const string MostHelpful = "most-helpful";

    /// <summary>Most recently edited first — where maintenance happens, for knowledge that rots.</summary>
    public const string RecentlyUpdated = "recently-updated";

    /// <summary>Engagement decayed by age. What is earning attention now, not what earned it once.</summary>
    public const string Trending = "trending";
}
