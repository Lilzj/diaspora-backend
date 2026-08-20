using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.Candidates;

/// <summary>
/// Where the pool comes from.
/// <para>
/// <b>The second half of the AI seam, and the half that is usually forgotten.</b> A vector database
/// or an embedding index does not rank a pool somebody else assembled — it <em>is</em> the retrieval
/// step, returning the two hundred nearest neighbours to a member's profile vector. A module whose
/// only extension point was <c>IRankingStrategy</c> would force such an adapter to re-rank whatever
/// a SQL query happened to hand it, which is the shape that makes people say a system needs
/// rewriting before it can use embeddings.
/// </para>
/// <para>
/// The shipped implementation reads the candidate projection with a coarse SQL query. A future
/// adapter registers alongside it: sources are additive, their results are merged and de-duplicated
/// by target reference, and a deployment can run both while it decides.
/// </para>
/// <para>
/// <b>A source may not rank, and the return type is what enforces it.</b> It returns candidates, not
/// scores — so a source that has an opinion about ordering has nowhere to put it, and the engine
/// stays the only thing that ranks.
/// </para>
/// </summary>
public interface IRecommendationCandidateSource
{
    /// <summary>The key this source is identified by in diagnostics and telemetry.</summary>
    string Key { get; }

    /// <summary>
    /// Lower runs first, and the first source to fill the pool wins.
    /// <para>
    /// Not a preference ordering: sources are merged, not raced. The order decides which source's
    /// projection survives de-duplication when two return the same target.
    /// </para>
    /// </summary>
    int Order => 100;

    /// <summary>Whether this source can serve a particular request. A vector source answers false with no index.</summary>
    bool CanServe(PersonalizationContext context) => true;

    /// <summary>
    /// Fetches a pool.
    /// <para>
    /// Unordered as far as the engine is concerned. See the class summary.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RecommendationCandidate>> FetchAsync(
        CandidateQuery query,
        PersonalizationContext context,
        CancellationToken cancellationToken = default);
}
