using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// The order a new arrival needs things in.
/// <para>
/// <b>The one strategy in the module that encodes a product opinion rather than a measurement, and
/// it says so.</b> Somebody who has just moved country needs to understand the paperwork before they
/// need a sofa. That is not derivable from any data the platform holds about them — they have not
/// done anything yet — and pretending otherwise by dressing it up as an engagement statistic would
/// make it harder to change when somebody disagrees.
/// </para>
/// <para>
/// <b>The ladder is the one the brief sets out</b> — country, guides, housing, jobs, organizations,
/// events, providers, marketplace — and it is a parameter, so a deployment launching in a country
/// where the platform's jobs coverage is its strongest asset can reorder it without a release.
/// </para>
/// <para>
/// <b>It runs only on the cold-start path.</b> Once a member has done anything at all, their own
/// behaviour is a better guide than a default ordering, and leaving this running would keep pushing
/// them back towards it.
/// </para>
/// </summary>
public sealed class ColdStartRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "cold-start";

    /// <summary>The ladder, as a comma-separated list of target type names.</summary>
    public const string LadderParameter = "ladder";

    /// <summary>
    /// The shipped ordering. See the class summary — a product opinion, deliberately visible.
    /// </summary>
    public static readonly IReadOnlyList<RecommendationTargetType> DefaultLadder =
    [
        RecommendationTargetType.Guide,
        RecommendationTargetType.LearningPath,
        RecommendationTargetType.HousingListing,
        RecommendationTargetType.Job,
        RecommendationTargetType.Organization,
        RecommendationTargetType.Event,
        RecommendationTargetType.Provider,
        RecommendationTargetType.MarketplaceListing,
        RecommendationTargetType.Neighborhood,
        RecommendationTargetType.Community
    ];

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.ColdStart;

    public bool SupportsColdStart => true;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scores = new List<RankingScore>(context.Candidates.Count);

        if (!context.Personalization.IsColdStart)
        {
            for (var index = 0; index < context.Candidates.Count; index++)
            {
                scores.Add(RankingScore.None());
            }

            return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
        }

        var ladder = ParseLadder(context.Configuration.Parameter(LadderParameter));

        foreach (var candidate in context.Candidates)
        {
            var rank = ladder.IndexOf(candidate.Target.TargetType);

            if (rank < 0)
            {
                scores.Add(RankingScore.None());
                continue;
            }

            // Linear rather than exponential: the ladder is an ordering, not a claim that guides
            // matter ten times more than jobs. An exponential fall-off would make the bottom half of
            // the ladder unreachable however good an individual candidate was.
            var value = decimal.Round(1m - ((decimal)rank / ladder.Count), 6);

            scores.Add(new RankingScore
            {
                Value = value,
                ReasonCode = rank == 0
                    ? RecommendationReasonCode.StartHere
                    : RecommendationReasonCode.PopularWithNewArrivals,
                SignalKinds = []
            });
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }

    private static List<RecommendationTargetType> ParseLadder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [.. DefaultLadder];
        }

        var parsed = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<RecommendationTargetType>(name, ignoreCase: true, out var type)
                ? (RecommendationTargetType?)type
                : null)
            .Where(type => type is not null)
            .Select(type => type!.Value)
            .Distinct()
            .ToList();

        // A ladder that parsed to nothing is a configuration mistake, and falling back to the
        // shipped order is better than scoring every candidate zero and returning an empty first
        // page to every new member on the platform.
        return parsed.Count == 0 ? [.. DefaultLadder] : parsed;
    }
}
