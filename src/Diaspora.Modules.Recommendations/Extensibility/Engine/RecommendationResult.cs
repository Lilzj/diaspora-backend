using Diaspora.Modules.Recommendations.Domain.Identifiers;

namespace Diaspora.Modules.Recommendations.Extensibility.Engine;

/// <summary>
/// What one generation produced, and how.
/// <para>
/// <b>The diagnostics travel with the answer rather than to a log, because they are the answer to
/// the second question.</b> "Here are four jobs" is the first; "out of a pool of two hundred, of
/// which a hundred and ninety-six were excluded by the country-visibility policy" is the one an
/// operator needs and cannot reconstruct. Logging it would put it somewhere nobody correlates with
/// the request that produced it.
/// </para>
/// <para>
/// <see cref="SetId"/> is null for a transient generation — the search-boost path — and every
/// analytics consumer treats that as "not a page", which is what keeps acceptance rates honest.
/// </para>
/// </summary>
public sealed record RecommendationResult
{
    /// <summary>The page, best first.</summary>
    public required IReadOnlyList<ScoredRecommendation> Items { get; init; }

    /// <summary>Null when the generation was transient. See the class summary.</summary>
    public RecommendationSetId? SetId { get; init; }

    /// <summary>Whether the cold-start path ran.</summary>
    public required bool IsColdStart { get; init; }

    /// <summary>Whether a stored set was reused rather than ranked again.</summary>
    public bool WasCached { get; init; }

    /// <summary>How many candidates the pool held before policies ran.</summary>
    public required int CandidateCount { get; init; }

    /// <summary>How many a policy removed, by policy key.</summary>
    public required IReadOnlyDictionary<string, int> ExclusionsByPolicy { get; init; }

    /// <summary>Which strategies contributed a non-zero score to at least one item.</summary>
    public required IReadOnlyList<string> StrategyKeys { get; init; }

    public required int GenerationMilliseconds { get; init; }

    public int ExcludedCount => ExclusionsByPolicy.Values.Sum();

    /// <summary>
    /// A generation that found nothing.
    /// <para>
    /// A first-class outcome rather than an error. A member in a country the platform has no content
    /// for yet gets an empty page and an honest one, and every caller handles it the same way.
    /// </para>
    /// </summary>
    public static RecommendationResult Empty(bool isColdStart) =>
        new()
        {
            Items = [],
            IsColdStart = isColdStart,
            CandidateCount = 0,
            ExclusionsByPolicy = new Dictionary<string, int>(StringComparer.Ordinal),
            StrategyKeys = [],
            GenerationMilliseconds = 0
        };
}
