using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Ranking;

namespace Diaspora.Modules.Jobs.Infrastructure.Ranking;

/// <summary>
/// What a browse uses: recency, reachability and trust, blended.
/// <para>
/// <b>The weights are a product decision, and stating them plainly is the point of having a
/// seam.</b> Reachability leads because a job a candidate cannot take is worth nothing to them
/// however fresh or reputable it is. Recency comes next because a stale advert is usually a
/// filled one. Trust is third — it matters enormously, but it is already enforced as a filter
/// and as a badge, so its job here is to break ties rather than to dominate.
/// </para>
/// <para>
/// Featured placement is added on top rather than blended in, and capped, so a paid or curated
/// slot can lift a posting but can never bury a better-matched one. A marketplace where money
/// outranked relevance would stop being useful to the people it exists for, which is the failure
/// every job board eventually has.
/// </para>
/// <para>
/// It reuses the other three strategies' own scoring functions rather than reimplementing them:
/// a blend that drifted from its components would be impossible to reason about, and the
/// duplicate decay curve is exactly the kind of thing that drifts.
/// </para>
/// </summary>
public sealed class BalancedRanking : IJobRankingStrategy
{
    private const double ReachabilityWeight = 0.45d;
    private const double RecencyWeight = 0.35d;
    private const double TrustWeight = 0.20d;

    /// <summary>Capped so placement lifts and never dominates. See the class summary.</summary>
    private const double FeaturedBonus = 0.15d;

    public string Key => JobRankingKeys.Balanced;

    public string Description =>
        "Reachability, recency and trust blended, with a capped bonus for featured placement.";

    public double Score(JobPosting posting, JobRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(context);

        var score =
            (ReachabilityWeight * ReachabilityRanking.Reachability(posting, context))
            + (RecencyWeight * RecencyRanking.DecayedFreshness(posting, context.Now))
            + (TrustWeight * TrustedRanking.Trustworthiness(posting));

        if (posting.IsFeatured)
        {
            score += FeaturedBonus;
        }

        return score;
    }
}
