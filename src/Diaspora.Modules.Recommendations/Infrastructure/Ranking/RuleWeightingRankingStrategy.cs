using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// The manual override, as data.
/// <para>
/// <b>Every recommendation platform grows one of these, and the only question is whether it is a
/// catalogue row or a special case somebody wrote into the engine at four in the afternoon.</b> The
/// legitimate needs are real: a country launch where the platform wants its own onboarding guides
/// surfaced regardless of engagement; a featured flag a vertical publishes and expects to mean
/// something; a category a deployment has decided to promote for a fortnight. Refusing to provide
/// this does not stop those needs — it just relocates them somewhere unauditable.
/// </para>
/// <para>
/// <b>Three rules, matched most-specific-first, all read from the catalogue row.</b> A boost for a
/// specific target, a boost for a category, and a boost for anything the owning module marked
/// featured. Nothing here is compiled in.
/// </para>
/// <para>
/// <b>It boosts and never suppresses, because suppression is a policy.</b> A negative rule here
/// would be an exclusion that never appeared in the exclusion diagnostics — a candidate quietly
/// scored to the bottom of every page with nothing recording that anybody decided so.
/// </para>
/// </summary>
public sealed class RuleWeightingRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "rule-weighting";

    /// <summary>Comma-separated target identifiers to boost.</summary>
    public const string BoostedTargetsParameter = "boostedtargets";

    /// <summary>Comma-separated category keys to boost.</summary>
    public const string BoostedCategoriesParameter = "boostedcategories";

    /// <summary>What a featured candidate scores. Zero disables the featured rule entirely.</summary>
    public const string FeaturedScoreParameter = "featuredscore";

    /// <summary>
    /// What a paid placement's own weight is multiplied by. Zero disables paid placement's effect on
    /// ranking entirely, without B15 having to stop selling it or this module having to stop
    /// projecting it.
    /// <para>
    /// <b>A multiplier rather than a fixed score, because B15 already priced the difference.</b> A
    /// premium listing, a featured provider and a boost are three products at three prices carrying
    /// three <c>PriorityWeight</c> values, and flattening them to one number here would discard the
    /// distinction the member paid for.
    /// </para>
    /// </summary>
    public const string PromotionScoreParameter = "promotionscore";

    private const decimal DefaultFeaturedScore = 0.6m;
    private const decimal TargetBoostScore = 1.0m;
    private const decimal CategoryBoostScore = 0.8m;

    /// <summary>
    /// The default multiplier on a paid placement's weight.
    /// <para>
    /// <b>0.9, so a purchase can never quite reach an operator's explicit instruction.</b> A named
    /// target scores 1.0 and a promoted category 0.8; a placement at full weight lands between them.
    /// That ordering is deliberate: money buys a strong position and never buys the top one, because
    /// the top one is where a platform puts the onboarding guide a new arrival needs.
    /// </para>
    /// </summary>
    private const decimal DefaultPromotionScore = 0.9m;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.RuleWeighting;

    public bool SupportsColdStart => true;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var boostedTargets = ParseSet(context.Configuration.Parameter(BoostedTargetsParameter));
        var boostedCategories = ParseSet(context.Configuration.Parameter(BoostedCategoriesParameter));
        var featuredScore = context.Configuration.ParameterOrDefault(
            FeaturedScoreParameter, DefaultFeaturedScore);

        var promotionScore = context.Configuration.ParameterOrDefault(
            PromotionScoreParameter, DefaultPromotionScore);

        // One instant for the whole page. Read once rather than per candidate, so a placement that
        // lapses between the first candidate and the four-hundredth is not judged against a different
        // moment from its neighbours — which would produce a page whose ordering cannot be reproduced.
        var asOf = context.Personalization.AsOf;

        var scores = new List<RankingScore>(context.Candidates.Count);

        foreach (var candidate in context.Candidates)
        {
            // Most specific first. A target named outright is a stronger instruction than its
            // category being promoted, which is stronger than a vertical's featured flag.
            if (boostedTargets.Contains(candidate.Target.TargetId.ToString("D")))
            {
                scores.Add(new RankingScore
                {
                    Value = TargetBoostScore,
                    ReasonCode = RecommendationReasonCode.StartHere,
                    SignalKinds = []
                });

                continue;
            }

            if (candidate.CategoryKey is not null && boostedCategories.Contains(candidate.CategoryKey))
            {
                scores.Add(new RankingScore
                {
                    Value = CategoryBoostScore,
                    ReasonCode = RecommendationReasonCode.MatchesCategory,
                    ReasonDetail = candidate.CategoryKey,
                    SignalKinds = []
                });

                continue;
            }

            // Above featured and below the two operator instructions. A member's purchase outranks a
            // moderator's free editorial flag — otherwise the flag would be worth more than the
            // product B15 sells — and never outranks a deployment naming a target outright.
            if (promotionScore > 0m && candidate.IsPromotedAt(asOf))
            {
                scores.Add(new RankingScore
                {
                    Value = Math.Clamp(candidate.PromotionWeight * promotionScore, 0m, 1m),

                    // Its own code, never HighlyRated. A promotion that explained itself as merit
                    // would be the platform telling a member something untrue about its own ordering.
                    ReasonCode = RecommendationReasonCode.Promoted,
                    SignalKinds = []
                });

                continue;
            }

            if (candidate.IsFeatured && featuredScore > 0m)
            {
                scores.Add(new RankingScore
                {
                    Value = Math.Clamp(featuredScore, 0m, 1m),
                    ReasonCode = RecommendationReasonCode.HighlyRated,
                    SignalKinds = []
                });

                continue;
            }

            scores.Add(RankingScore.None());
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }

    private static HashSet<string> ParseSet(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
}
