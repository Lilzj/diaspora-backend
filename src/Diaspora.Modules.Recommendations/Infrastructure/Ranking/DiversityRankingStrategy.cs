using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// Rewards a candidate for being unlike what the member already sees.
/// <para>
/// <b>Not the same thing as the diversity pass, and the two exist together deliberately.</b>
/// <c>GroupCappingDiversifier</c> works on the assembled page and stops one employer taking twenty
/// slots; this works on the pool and rewards a category the member has been under-served. The first
/// prevents a bad page. The second prevents a bad month — a member whose first week produced housing
/// signals being shown housing forever, because every other strategy is a feedback loop pointed at
/// what they already did.
/// </para>
/// <para>
/// <b>The novelty is measured against the member's own activity, not against the pool.</b> A
/// category the platform has plenty of but this member has never engaged with is exactly what wants
/// surfacing; a category that is rare overall is not automatically interesting.
/// </para>
/// <para>
/// <b>A type the member reliably dismisses is scored down rather than up.</b> Novelty is not an
/// excuse to keep showing somebody the thing they keep refusing, and without this the strategy would
/// do precisely that — a dismissed category has low engagement, which reads as under-served.
/// </para>
/// </summary>
public sealed class DiversityRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "diversity";

    /// <summary>Above this dismissal rate the novelty bonus is withdrawn. See the class summary.</summary>
    public const string DismissalCeilingParameter = "dismissalceiling";

    private const decimal DefaultDismissalCeiling = 0.4m;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.Diversity;

    public bool SupportsColdStart => false;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var personalization = context.Personalization;
        var scores = new List<RankingScore>(context.Candidates.Count);

        if (!personalization.IsSurfacePersonalized)
        {
            for (var index = 0; index < context.Candidates.Count; index++)
            {
                scores.Add(RankingScore.None());
            }

            return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
        }

        var dismissalCeiling = context.Configuration.ParameterOrDefault(
            DismissalCeilingParameter, DefaultDismissalCeiling);

        var profile = personalization.Profile;

        // The busiest type the member has, as the yardstick. Novelty is relative: in a profile whose
        // heaviest type has three interactions, one interaction is not neglect.
        var busiest = RankingMath.MaxOrZero(
            profile.ActivitySummaries.Select(summary => (decimal)summary.TotalCount));

        foreach (var candidate in context.Candidates)
        {
            var summary = profile.ActivityFor(candidate.Target.TargetType);

            if (summary is not null && summary.DismissalRate > dismissalCeiling)
            {
                // They keep saying no. Novelty is not a reason to ask again.
                scores.Add(RankingScore.None());
                continue;
            }

            var engagement = summary?.TotalCount ?? 0;
            var novelty = busiest <= 0m
                ? 0m
                : decimal.Round(1m - RankingMath.Normalize(engagement, busiest), 6);

            // A category the member has an affinity for is not novel, whatever the type-level counts
            // say. Without this the strategy would boost the member's favourite category on the
            // grounds that its target type is under-used.
            if (candidate.CategoryKey is not null &&
                profile.AffinityFor(InterestKind.Category, candidate.CategoryKey) > 0.5m)
            {
                novelty = decimal.Round(novelty * 0.5m, 6);
            }

            if (novelty <= 0m)
            {
                scores.Add(RankingScore.None());
                continue;
            }

            scores.Add(new RankingScore
            {
                Value = novelty,
                ReasonCode = RecommendationReasonCode.MatchesInterests,
                ReasonDetail = candidate.CategoryKey,
                SignalKinds = [SignalKind.Viewed]
            });
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }
}
