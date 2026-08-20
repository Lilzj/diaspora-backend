using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// How much attention a candidate is already getting.
/// <para>
/// <b>The strategy that works with no profile at all, which is why it is one of the four that runs on
/// the cold-start path.</b> It says nothing about the member and everything about the pool, and for
/// somebody the platform has never observed that is the most honest thing it can offer.
/// </para>
/// <para>
/// <b>Scored against the pool rather than against a constant.</b> Engagement counts differ by two
/// orders of magnitude between verticals — a guide read is not a flat viewing — so an absolute
/// threshold would make one vertical permanently popular and another permanently obscure. See
/// <see cref="RankingMath.Normalize"/>.
/// </para>
/// <para>
/// <b>The projected popularity score and the raw engagement count are blended rather than one being
/// chosen.</b> The projected score is the owning module's own judgement, normalised inside its own
/// vertical, and it is the better signal where a module publishes one; the raw count is the fallback
/// where a module publishes nothing but a number. Using only the count would ignore Housing's view
/// that a viewing request is worth more than a page view; using only the score would drop every
/// vertical that has not started publishing one.
/// </para>
/// </summary>
public sealed class PopularityRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "popularity";

    /// <summary>How much of the score comes from the owning module's own judgement.</summary>
    private const decimal ProjectedWeight = 0.6m;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.Popularity;

    public bool SupportsColdStart => true;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = context.Candidates;
        var maximumEngagement = RankingMath.MaxOrZero(candidates.Select(c => (decimal)c.EngagementCount));

        var scores = new List<RankingScore>(candidates.Count);
        var country = context.Personalization.EffectiveCountryCode;

        foreach (var candidate in candidates)
        {
            var engagement = RankingMath.Normalize(candidate.EngagementCount, maximumEngagement);
            var value = decimal.Round(
                (candidate.PopularityScore * ProjectedWeight) + (engagement * (1m - ProjectedWeight)), 6);

            if (value <= 0m)
            {
                scores.Add(RankingScore.None());
                continue;
            }

            // The reason is chosen by how specific the platform can honestly be about where the
            // popularity was observed. Saying "popular in your neighbourhood" about a national
            // figure would be the kind of small dishonesty that costs the whole explanation its
            // credibility the first time somebody checks.
            var (reason, detail) = ChooseReason(
                candidate.NeighborhoodName,
                candidate.CityName,
                candidate.CountryCode,
                country,
                context.Personalization.Profile.PrimaryCityName,
                context.Personalization.Profile.PrimaryNeighborhoodName);

            scores.Add(new RankingScore
            {
                Value = value,
                ReasonCode = reason,
                ReasonDetail = detail,
                SignalKinds = []
            });
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }

    private static (RecommendationReasonCode Reason, string? Detail) ChooseReason(
        string? candidateNeighborhood,
        string? candidateCity,
        string? candidateCountry,
        string? memberCountry,
        string? memberCity,
        string? memberNeighborhood)
    {
        if (Matches(candidateNeighborhood, memberNeighborhood))
        {
            return (RecommendationReasonCode.PopularInNeighborhood, candidateNeighborhood);
        }

        if (Matches(candidateCity, memberCity))
        {
            return (RecommendationReasonCode.PopularInCity, candidateCity);
        }

        if (Matches(candidateCountry, memberCountry))
        {
            return (RecommendationReasonCode.PopularInCountry, candidateCountry);
        }

        return (RecommendationReasonCode.TrendingNow, null);
    }

    private static bool Matches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
