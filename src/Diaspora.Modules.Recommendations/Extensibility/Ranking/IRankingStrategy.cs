using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Ranking;

/// <summary>
/// One way of deciding how good a candidate is for a member.
/// <para>
/// <b>This is the module's principal extension point and the one the whole AI-readiness claim rests
/// on.</b> A future machine-learned ranker, a vector-similarity adapter or an LLM re-ranker is a
/// class implementing this interface, registered in a deployment's own assembly, weighted by a
/// catalogue row against the deterministic strategies that ship today. No engine change, no schema
/// change, no API change. That is what "no architectural restructuring" has to mean if it means
/// anything.
/// </para>
/// <para>
/// <b>Three obligations, and they are what make an adapter safe to plug in.</b> A strategy must
/// return a score for every candidate it was given, in the same order — a total function, so the
/// engine never has to guess what silence meant. It must return a value between 0 and 1, because a
/// score outside that range would out-weigh its own catalogue row and break the arithmetic every
/// explanation is built from. And it must pair every non-zero score with a reason, because a page
/// nobody can explain is the one outcome this module exists to prevent.
/// </para>
/// <para>
/// <b>Asynchronous deliberately, though every shipped implementation is synchronous.</b> A vector
/// search or a model call is a network round trip, and a seam that returned
/// <c>IReadOnlyList&lt;RankingScore&gt;</c> would force every such adapter to block a thread — which
/// is the sort of thing that is free to fix now and a rewrite later.
/// </para>
/// <para>
/// <b>A strategy may not exclude.</b> Returning zero demotes; it does not remove. Removal is a
/// policy's job, and the separation is asserted by an architecture test — see
/// <see cref="RecommendationPolicyKind"/> for why the distinction is load-bearing.
/// </para>
/// </summary>
public interface IRankingStrategy
{
    /// <summary>The key a catalogue row resolves this by. Unique across the platform.</summary>
    string Key { get; }

    /// <summary>The family this belongs to. The catalogue validates a row's kind against this.</summary>
    RankingStrategyKind Kind { get; }

    /// <summary>
    /// Whether this strategy can say anything useful about a member with no history.
    /// <para>
    /// A similarity strategy answers false: given an empty profile it returns zero for every
    /// candidate, and a strategy that contributes nothing but is listed as having run makes an
    /// explanation misleading.
    /// </para>
    /// </summary>
    bool SupportsColdStart => false;

    /// <summary>
    /// Scores every candidate in the pool.
    /// <para>
    /// One score per candidate, in the order given. See the class summary for the three obligations
    /// an implementation is held to.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this strategy applies to a candidate at all.
    /// <para>
    /// Defaults to true. A strategy that only understands one target type overrides it rather than
    /// returning zero, so the engine can leave it out of the explanation instead of recording that
    /// it ran and found nothing.
    /// </para>
    /// </summary>
    bool AppliesTo(RecommendationCandidate candidate) => true;
}
