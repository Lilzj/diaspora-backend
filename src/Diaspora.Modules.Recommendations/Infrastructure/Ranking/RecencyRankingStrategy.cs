using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// How new a candidate is.
/// <para>
/// <b>The strategy whose correct weight varies most between surfaces, which is the clearest
/// argument for weights being data.</b> On the jobs surface a two-week-old posting is probably
/// filled and recency deserves a high weight; on the guides surface the best answer to "how do I
/// renew a residence permit" is often four years old and carefully maintained, and a high recency
/// weight would bury it under a thin post from yesterday.
/// </para>
/// <para>
/// <b>The half-life is a parameter rather than a constant, and it is read from the catalogue
/// row.</b> Thirty days is the shipped default. The jobs surface seeds seven; the guides surface
/// seeds three hundred and sixty-five, which is close enough to "recency barely matters" to be worth
/// saying explicitly rather than by setting the weight to zero — a zero weight would remove the
/// contribution from the explanation, and "recently updated" is a genuinely useful thing to be able
/// to say about a guide.
/// </para>
/// <para>
/// <b>Updates count, not just publications.</b> A guide revised last week is fresh in the sense that
/// matters, and treating only publication as recency would mean the platform's best-maintained
/// content aged exactly as fast as its most neglected.
/// </para>
/// </summary>
public sealed class RecencyRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "recency";

    /// <summary>The catalogue parameter that overrides the half-life.</summary>
    public const string HalfLifeParameter = "halflifedays";

    private const double DefaultHalfLifeDays = 30d;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.Recency;

    public bool SupportsColdStart => true;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var halfLife = (double)context.Configuration.ParameterOrDefault(
            HalfLifeParameter, (decimal)DefaultHalfLifeDays);

        if (halfLife <= 0)
        {
            halfLife = DefaultHalfLifeDays;
        }

        var asOf = context.Personalization.AsOf;
        var scores = new List<RankingScore>(context.Candidates.Count);

        foreach (var candidate in context.Candidates)
        {
            // Whichever is later. See the class summary — a maintained guide is fresh.
            var published = RankingMath.Freshness(candidate.PublishedAt, asOf, halfLife);
            var updated = RankingMath.Freshness(candidate.SourceUpdatedAt, asOf, halfLife);

            var value = Math.Max(published, updated);

            if (value <= 0m)
            {
                scores.Add(RankingScore.None());
                continue;
            }

            scores.Add(new RankingScore
            {
                Value = value,
                ReasonCode = updated > published
                    ? RecommendationReasonCode.RecentlyUpdated
                    : RecommendationReasonCode.RecentlyPublished,
                SignalKinds = []
            });
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }
}
