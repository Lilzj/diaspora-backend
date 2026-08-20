using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.Explainability;

/// <summary>
/// Turns an arithmetic result into a sentence a person would accept.
/// <para>
/// <b>Separate from the strategies because the best explanation is rarely the biggest
/// contribution.</b> A guide that scored highest on popularity, second on location and third on
/// language is honestly described as "popular in your neighbourhood" — the location contribution is
/// what makes the popularity relevant, and "popular" alone reads as "we showed you the most clicked
/// thing", which is both less useful and less true.
/// </para>
/// <para>
/// <b>Replaceable, and the reason is that this is the most product-sensitive component in the
/// module.</b> Which reason to lead with, how much detail to include, how honest to be when the
/// answer is "we do not know you yet" — these are editorial decisions that will be revised more
/// often than anything else here, and they should be revisable without touching ranking.
/// </para>
/// </summary>
public interface IRecommendationReasonBuilder
{
    /// <summary>
    /// Chooses and builds the explanation for one recommendation.
    /// <para>
    /// Never returns null. When nothing better can be said the honest answer is the fallback reason,
    /// and an implementation that returned nothing would produce the unexplained card the module
    /// exists to prevent.
    /// </para>
    /// </summary>
    RecommendationExplanation Build(
        RecommendationCandidate candidate,
        IReadOnlyList<StrategyContribution> contributions,
        IReadOnlyDictionary<string, Ranking.RankingScore> scoresByStrategy,
        PersonalizationContext context);
}
