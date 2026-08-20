namespace Diaspora.Modules.Jobs.Extensibility.Ranking;

/// <summary>
/// The ranking strategies the platform ships with.
/// <para>
/// Constants rather than an enum, for the reason the whole seam exists: a deployment or a later
/// module registers a strategy under a key of its own, and an enum here would make that a change
/// to this module.
/// </para>
/// </summary>
public static class JobRankingKeys
{
    /// <summary>Most recently published first. The honest default.</summary>
    public const string Recency = "recency";

    /// <summary>Weighted toward postings the viewer could actually take.</summary>
    public const string Reachability = "reachability";

    /// <summary>Weighted toward employers carrying a Trust mark.</summary>
    public const string Trusted = "trusted";

    /// <summary>A blend of recency, reachability and trust. What a browse uses.</summary>
    public const string Balanced = "balanced";
}
