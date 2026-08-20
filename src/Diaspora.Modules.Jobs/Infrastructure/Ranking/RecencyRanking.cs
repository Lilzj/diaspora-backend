using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Ranking;

namespace Diaspora.Modules.Jobs.Infrastructure.Ranking;

/// <summary>
/// Newest first, with a gentle decay.
/// <para>
/// <b>The honest default.</b> On a job board recency is not a proxy for quality — it is the
/// single most useful signal a candidate has, because an advert posted three months ago is
/// usually filled and nobody has taken it down.
/// </para>
/// <para>
/// The decay is exponential with a two-week half-life, so a fortnight-old posting scores half a
/// fresh one and a three-month-old posting is far below both without ever being excluded. A hard
/// cutoff would be worse: a genuinely open role that has been hard to fill is exactly the one a
/// candidate might want.
/// </para>
/// </summary>
public sealed class RecencyRanking : IJobRankingStrategy
{
    private const double HalfLifeDays = 14d;

    public string Key => JobRankingKeys.Recency;

    public string Description => "Most recently published first, decaying over a fortnight.";

    public double Score(JobPosting posting, JobRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(context);

        return DecayedFreshness(posting, context.Now);
    }

    /// <summary>
    /// A 0..1 freshness score. Shared with the balanced strategy, which weights it against
    /// other signals rather than recomputing a second, subtly different decay.
    /// </summary>
    internal static double DecayedFreshness(JobPosting posting, DateTimeOffset now)
    {
        var published = posting.PublishedAt ?? posting.CreatedAt;
        var ageDays = Math.Max(0d, (now - published).TotalDays);

        return Math.Pow(0.5d, ageDays / HalfLifeDays);
    }
}
