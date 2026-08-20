using Diaspora.Modules.Recommendations.Domain.Candidates;

namespace Diaspora.Modules.Recommendations.Extensibility.Similarity;

/// <summary>
/// How alike two things are.
/// <para>
/// <b>The narrowest of the three AI seams, and the most obviously shaped for what comes next.</b>
/// The shipped implementation computes a weighted Jaccard overlap of attributes, categories and
/// places — deterministic, explainable, and free. An embedding-based implementation computes cosine
/// similarity between two vectors. Both answer the same question and return the same kind of number,
/// so the similarity strategy above them does not change at all.
/// </para>
/// <para>
/// <b>Batched rather than pairwise, because the pairwise version is the one that cannot be made
/// fast.</b> A member with ten recently-viewed items against a pool of four hundred is four thousand
/// comparisons; as four thousand calls that is four thousand round trips for a vector adapter, and
/// as one call it is one query. The seam that ships is the seam an adapter can implement well.
/// </para>
/// </summary>
public interface ISimilarityProvider
{
    /// <summary>The key this provider is identified by in diagnostics.</summary>
    string Key { get; }

    /// <summary>
    /// How similar each candidate is to a set of reference candidates.
    /// <para>
    /// Returns one score per candidate, in the order given, each between 0 and 1. An empty reference
    /// set returns zeroes rather than throwing — a member with no history is the normal case on this
    /// path, not an error.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<decimal>> ScoreAsync(
        IReadOnlyList<RecommendationCandidate> candidates,
        IReadOnlyList<RecommendationCandidate> references,
        CancellationToken cancellationToken = default);
}
