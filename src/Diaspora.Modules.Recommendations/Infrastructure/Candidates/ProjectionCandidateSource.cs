using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Infrastructure.Candidates;

/// <summary>
/// The shipped candidate source: the projection table, read with a coarse query.
/// <para>
/// <b>Deliberately thin, because everything interesting happens above it.</b> A source that narrowed
/// cleverly would be a ranking implementation with no explanation attached, and the two would
/// immediately disagree about what a good candidate is. This one filters on country, type, activity
/// and trust, and stops.
/// </para>
/// <para>
/// <b>It is one implementation of a plural seam.</b> A future vector adapter registers alongside
/// rather than instead, its results are merged with these by target reference, and a deployment runs
/// both while it decides. That is the shape that makes the retrieval half of the AI seam real — see
/// <see cref="IRecommendationCandidateSource"/>.
/// </para>
/// </summary>
public sealed class ProjectionCandidateSource : IRecommendationCandidateSource
{
    public const string SourceKey = "projection";

    private readonly IRecommendationCandidateRepository _candidates;

    public ProjectionCandidateSource(IRecommendationCandidateRepository candidates) =>
        _candidates = candidates;

    public string Key => SourceKey;

    /// <summary>Runs first, so its projection wins de-duplication against any adapter added later.</summary>
    public int Order => 0;

    public async Task<IReadOnlyList<RecommendationCandidate>> FetchAsync(
        CandidateQuery query,
        PersonalizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await _candidates.SearchAsync(query, cancellationToken);
    }
}
