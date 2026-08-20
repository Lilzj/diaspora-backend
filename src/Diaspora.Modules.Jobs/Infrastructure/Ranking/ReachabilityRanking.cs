using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Ranking;

namespace Diaspora.Modules.Jobs.Infrastructure.Ranking;

/// <summary>
/// Weighted toward postings the viewer could actually take.
/// <para>
/// <b>The strategy this platform exists for.</b> A candidate in Lagos scrolling jobs in Berlin
/// is looking at two very different lists: the ones they could start next month, and the ones
/// that would require an employer to sponsor them. Both belong in the results — a candidate is
/// entitled to see the whole market — but the reachable ones belong nearer the top.
/// </para>
/// <para>
/// <b>It only ever lifts; it never buries.</b> Every component adds to the score and none
/// subtracts, and no posting is excluded. That asymmetry is deliberate: a ranking seam that could
/// push a posting down for a candidate's nationality or visa status is a discrimination surface
/// wearing a relevance badge, and the way to keep it from becoming one is to make burying
/// inexpressible rather than merely discouraged.
/// </para>
/// </summary>
public sealed class ReachabilityRanking : IJobRankingStrategy
{
    public string Key => JobRankingKeys.Reachability;

    public string Description =>
        "Lifts postings the viewer could take — their country, their skills, sponsored roles.";

    public double Score(JobPosting posting, JobRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(context);

        return Reachability(posting, context);
    }

    /// <summary>
    /// A 0..1 reachability score. Shared with the balanced strategy so the two cannot drift.
    /// </summary>
    internal static double Reachability(JobPosting posting, JobRankingContext context)
    {
        var score = 0d;

        // Remote work is reachable from anywhere, which is the whole reason it matters here.
        if (posting.WorkArrangement.IsRemote)
        {
            score += 0.30d;
        }

        // Already where the job is.
        if (context.ViewerCountryCode is { Length: 2 } viewerCountry
            && string.Equals(
                posting.Location.CountryCode, viewerCountry, StringComparison.Ordinal))
        {
            score += 0.30d;
        }

        // Somewhere the candidate said they would move to.
        else if (context.PreferredCountries.Contains(
            posting.Location.CountryCode, StringComparer.Ordinal))
        {
            score += 0.20d;
        }

        // Sponsorship only lifts, and only for someone who needs it. A candidate who does not
        // need sponsorship gains nothing from a posting that offers it, and — critically — a
        // candidate who does need it loses nothing from a posting that does not.
        if (context.NeedsSponsorship && posting.WorkArrangement.OffersVisaSponsorship)
        {
            score += 0.25d;
        }

        // Skill overlap, as a share of what the posting asks for. Capped, because a posting
        // naming two skills the candidate happens to have should not outrank one naming ten.
        if (context.SkillKeys.Count > 0 && posting.Skills.Count > 0)
        {
            var matched = posting.Skills.Count(skill =>
                context.SkillKeys.Contains(skill.SkillKey, StringComparer.Ordinal));

            score += 0.15d * Math.Min(1d, matched / (double)posting.Skills.Count);
        }

        return Math.Min(1d, score);
    }
}
