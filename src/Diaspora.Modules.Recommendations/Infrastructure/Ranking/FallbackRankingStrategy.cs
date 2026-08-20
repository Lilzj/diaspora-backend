using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// A small score for everything, so no page is ever empty for an arithmetic reason.
/// <para>
/// <b>It exists because "every strategy returned zero" and "there is nothing to show" are different
/// situations that produce the same empty page.</b> A member in a country the platform has just
/// launched in, on a surface whose strategies all need history, would otherwise see nothing at all —
/// not because there is nothing, but because nothing scored. This guarantees that a candidate which
/// survived every policy is shown rather than discarded by a rounding.
/// </para>
/// <para>
/// <b>Its weight is deliberately tiny and its reason is deliberately honest.</b> At a weight of 0.05
/// it never reorders anything a real strategy had an opinion about; it only decides between
/// candidates that are otherwise tied at zero. And it explains itself as "popular with new arrivals"
/// rather than inventing a personal-sounding reason, because the one thing worse than a weak
/// recommendation is a weak recommendation with a confident explanation.
/// </para>
/// <para>
/// <b>The tie-break is the projected popularity score, not the order the database returned.</b>
/// Database order is arbitrary and unstable, so two consecutive requests would produce two different
/// pages with no explanation for the change — which reads, correctly, as a system that is guessing.
/// </para>
/// </summary>
public sealed class FallbackRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "fallback";

    /// <summary>The floor every surviving candidate gets. Enough to beat zero, not enough to matter.</summary>
    private const decimal Floor = 0.1m;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.Fallback;

    public bool SupportsColdStart => true;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scores = new List<RankingScore>(context.Candidates.Count);

        foreach (var candidate in context.Candidates)
        {
            // The floor plus a fraction of the projected popularity, so ties break stably and the
            // same request twice produces the same page. See the class summary.
            var value = decimal.Round(
                Math.Clamp(Floor + (candidate.PopularityScore * (1m - Floor)), 0m, 1m), 6);

            scores.Add(new RankingScore
            {
                Value = value,
                ReasonCode = context.Personalization.IsColdStart
                    ? RecommendationReasonCode.PopularWithNewArrivals
                    : RecommendationReasonCode.TrendingNow,
                SignalKinds = []
            });
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }
}
