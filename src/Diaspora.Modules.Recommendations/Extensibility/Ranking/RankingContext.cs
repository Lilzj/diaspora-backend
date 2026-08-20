using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.Ranking;

/// <summary>
/// What a strategy is given for one ranking pass.
/// <para>
/// <b>The whole pool, not one candidate at a time, and that shape is what makes normalisation
/// possible.</b> "How popular is this job" has no answer in isolation — a hundred views is
/// extraordinary for a niche listing and unremarkable for a national one. A strategy that saw one
/// candidate could only score it against a constant, which is how absolute thresholds get baked in
/// and then quietly stop meaning anything as the platform grows.
/// </para>
/// <para>
/// <b><see cref="Configuration"/> is the strategy's own catalogue row.</b> A strategy reads its
/// weight, its order and its parameters from here rather than from the repository, which is what
/// lets one implementation serve every surface it is configured on.
/// </para>
/// </summary>
public sealed record RankingContext
{
    /// <summary>Everything known about the member. Assembled once — see <see cref="PersonalizationContext"/>.</summary>
    public required PersonalizationContext Personalization { get; init; }

    /// <summary>The pool, after policies have excluded and before anything has ranked.</summary>
    public required IReadOnlyList<RecommendationCandidate> Candidates { get; init; }

    /// <summary>This strategy's configuration for this surface. See the class summary.</summary>
    public required RankingStrategy Configuration { get; init; }

    /// <summary>
    /// Targets the member has already engaged with, by type-and-id key.
    /// <para>
    /// Supplied rather than queried per strategy: the similarity strategy needs it to find what to
    /// be similar to, and the diversity strategy needs it to avoid repeating. Two reads of the same
    /// thing in one pass is one read too many on a path that runs on every app launch.
    /// </para>
    /// </summary>
    public IReadOnlySet<string> EngagedTargetKeys { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);
}
