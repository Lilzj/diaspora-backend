using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Ranking;

namespace Diaspora.Modules.Jobs.Infrastructure.Ranking;

/// <summary>
/// Weighted toward postings a candidate can rely on.
/// <para>
/// <b>The fraud defence, expressed as a ranking rather than a filter.</b> The members of this
/// platform are routinely approached by strangers offering work abroad, which is the exact shape
/// of the most common fraud against them. A verified employer's posting near the top is worth
/// more here than on any general job board.
/// </para>
/// <para>
/// It ranks rather than filters because an unverified employer is not a fraudulent one — most
/// are simply new, and a platform that buried every new employer would have no new employers.
/// A candidate who wants only verified ones has a filter for that.
/// </para>
/// </summary>
public sealed class TrustedRanking : IJobRankingStrategy
{
    public string Key => JobRankingKeys.Trusted;

    public string Description =>
        "Lifts verified postings and postings that state what they pay.";

    public double Score(JobPosting posting, JobRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(context);

        return Trustworthiness(posting);
    }

    /// <summary>
    /// A 0..1 trust score. Shared with the balanced strategy so the two cannot drift.
    /// <para>
    /// The employer's own badge is not read here — a strategy scores a posting it was handed,
    /// and reaching into the employer aggregate per row would make ranking a page an N+1. The
    /// posting's own trust mark and the signals it carries are what is available, and the
    /// verified-employer <em>filter</em> is where the badge itself is enforced.
    /// </para>
    /// </summary>
    internal static double Trustworthiness(JobPosting posting)
    {
        var score = 0d;

        if (posting.TrustMark.IsVerified)
        {
            score += 0.50d;
        }

        // A published salary is the strongest honesty signal a posting can carry, and the one
        // fraudulent adverts most often omit. It is also simply what candidates want.
        if (posting.Salary.IsDisclosed)
        {
            score += 0.25d;
        }

        // A posting that says what the work is, in detail, is a posting somebody wrote.
        if (posting.Requirements.Count > 0 || posting.Responsibilities.Count > 0)
        {
            score += 0.15d;
        }

        // A closing date is a commitment. An advert with no end is one nobody is watching.
        if (posting.ClosingAt is not null)
        {
            score += 0.10d;
        }

        return Math.Min(1d, score);
    }
}
