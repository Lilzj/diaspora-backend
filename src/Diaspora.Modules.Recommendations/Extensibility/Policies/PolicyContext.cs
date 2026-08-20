using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.Policies;

/// <summary>
/// What a policy rule is given for one evaluation pass.
/// <para>
/// <b>The same shape as <see cref="Ranking.RankingContext"/>, and deliberately so.</b> A policy that
/// needed a different bundle of facts would tempt an implementer to fetch what it was missing, and a
/// policy that queries is a policy that runs a database read per candidate on the hottest path in
/// the module.
/// </para>
/// <para>
/// <b><see cref="SuppressedTargetKeys"/> and <see cref="OwnedTargetKeys"/> are supplied rather than
/// looked up, and that is the same decision.</b> Both are per-member sets read once per generation;
/// a dismissal policy that queried per candidate would issue four hundred reads for one page.
/// </para>
/// </summary>
public sealed record PolicyContext
{
    public required PersonalizationContext Personalization { get; init; }

    /// <summary>This rule's catalogue row: its effect, its factor, its parameters.</summary>
    public required RecommendationPolicy Configuration { get; init; }

    /// <summary>Targets the member's dismissals still suppress, by type-and-id key.</summary>
    public IReadOnlySet<string> SuppressedTargetKeys { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Targets the member owns, by type-and-id key. The self-authored rule's input.</summary>
    public IReadOnlySet<string> OwnedTargetKeys { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Authors the member has blocked, when Community has told this module about them.</summary>
    public IReadOnlySet<Guid> BlockedAuthorIds { get; init; } = new HashSet<Guid>();

    /// <summary>Convenience: the key form every set above is indexed by.</summary>
    public static string KeyFor(RecommendationCandidate candidate) => candidate.Target.Key;
}
