using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Ranking;
using Diaspora.Modules.Jobs.Infrastructure.Ranking;
using Diaspora.Modules.Jobs.Extensibility.Recommendations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Infrastructure.Recommendations;

/// <summary>
/// The shipped recommender: a filter over what the candidate already told the platform.
/// <para>
/// <b>It does not learn, score behaviour, or model anybody</b>, and that is a scope decision
/// rather than a limitation to be fixed quietly. AI candidate matching is explicitly out of
/// scope for B9. What this does is take a candidate's own stated skills, countries and
/// authorisations and find the open postings that fit them — which is a search the candidate
/// could have run themselves, run on their behalf.
/// </para>
/// <para>
/// <b>Every suggestion carries a reason a person can read and correct.</b> That is why
/// <c>JobRecommendation.Reason</c> is required on the contract: a recommendation nobody can
/// account for is one nobody can trust, and on a platform where the suggestion may amount to
/// "move country for this", an unexplained ranking is not good enough. Requiring it now, while
/// the implementation is a heuristic that can trivially produce one, is what stops a later model
/// shipping without it.
/// </para>
/// <para>
/// <b>An empty answer is always valid.</b> A candidate with no profile, or one whose criteria
/// match nothing, gets nothing rather than filler — irrelevant recommendations teach a member to
/// ignore the surface entirely, and that is expensive to undo.
/// </para>
/// </summary>
public sealed class HeuristicJobRecommendationProvider : IJobRecommendationProvider
{
    private readonly ICandidateProfileRepository _candidates;
    private readonly IJobPostingRepository _postings;
    private readonly IClock _clock;

    public HeuristicJobRecommendationProvider(
        ICandidateProfileRepository candidates,
        IJobPostingRepository postings,
        IClock clock)
    {
        _candidates = candidates;
        _postings = postings;
        _clock = clock;
    }

    public string Name => "Heuristic";

    public async Task<IReadOnlyList<JobRecommendation>> RecommendAsync(
        UserId userId,
        CandidateProfileId? candidateId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var profile = candidateId is { } id
            ? await _candidates.GetForReadAsync(id, cancellationToken)
            : await _candidates.GetByUserForReadAsync(userId, cancellationToken);

        // No profile, nothing to go on. Filler here would be worse than silence.
        if (profile is null)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var skillKeys = profile.Skills.Select(skill => skill.SkillKey).ToList();

        var preferredCountries = profile.PreferredLocations
            .Select(location => location.CountryCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var context = new JobRankingContext
        {
            ViewerCountryCode = profile.CountryCode,
            PreferredCountries = preferredCountries,
            NeedsSponsorship = profile.CountryCode is null
                || preferredCountries.Exists(country =>
                    profile.NeedsSponsorshipFor(country, today)),
            SkillKeys = skillKeys,
            Now = now
        };

        var candidates = await GatherAsync(profile, skillKeys, limit, cancellationToken);

        if (candidates.Count == 0)
        {
            return [];
        }

        return [.. candidates
            .Select(posting => new JobRecommendation
            {
                JobId = posting.Id.Value,
                Score = ReachabilityRanking.Reachability(posting, context),
                Reason = Explain(posting, profile, context, today)
            })
            .Where(recommendation => recommendation.Score > 0d)
            .OrderByDescending(recommendation => recommendation.Score)
            .Take(Math.Clamp(limit, 1, 50))];
    }

    /// <summary>
    /// Gathers a pool to score.
    /// <para>
    /// Two passes rather than one, and deliberately so: a skill query alone misses a candidate
    /// with no skills listed, and a location query alone misses a remote role in a country they
    /// never named. Over-fetching a little and scoring the union is cheaper and better than
    /// either query on its own — and the score, not the query, decides what survives.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<JobPosting>> GatherAsync(
        CandidateProfile profile,
        List<string> skillKeys,
        int limit,
        CancellationToken cancellationToken)
    {
        // Three times the ask, so the scoring pass has something to discriminate between.
        var pool = Math.Clamp(limit * 3, 10, 60);
        var gathered = new Dictionary<Guid, JobPosting>();

        if (skillKeys.Count > 0)
        {
            var bySkill = await _postings.QueryAsync(
                new JobQueryFilter
                {
                    SkillKeys = skillKeys,
                    OpenOnly = true,
                    Sort = JobSortOrder.Newest
                },
                cursor: null,
                pool,
                cancellationToken);

            foreach (var posting in bySkill.Items)
            {
                gathered[posting.Id.Value] = posting;
            }
        }

        var countries = profile.PreferredLocations
            .Select(location => location.CountryCode)
            .Concat(profile.CountryCode is null ? [] : [profile.CountryCode])
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();

        foreach (var country in countries)
        {
            var byCountry = await _postings.QueryAsync(
                new JobQueryFilter
                {
                    CountryCode = country,
                    OpenOnly = true,
                    Sort = JobSortOrder.Newest
                },
                cursor: null,
                pool,
                cancellationToken);

            foreach (var posting in byCountry.Items)
            {
                gathered[posting.Id.Value] = posting;
            }
        }

        return [.. gathered.Values];
    }

    /// <summary>
    /// The sentence the candidate reads. Leads with whichever signal actually earned the
    /// suggestion, because "matches your skills" on a posting that matched only on country is a
    /// small lie that costs the whole surface its credibility.
    /// </summary>
    private static string Explain(
        JobPosting posting,
        CandidateProfile profile,
        JobRankingContext context,
        DateOnly today)
    {
        var matchedSkills = posting.Skills.Count(skill =>
            context.SkillKeys.Contains(skill.SkillKey, StringComparer.Ordinal));

        if (matchedSkills > 0)
        {
            return matchedSkills == 1
                ? "Matches one of the skills on your profile"
                : $"Matches {matchedSkills} of the skills on your profile";
        }

        if (context.NeedsSponsorship
            && posting.WorkArrangement.OffersVisaSponsorship
            && profile.NeedsSponsorshipFor(posting.Location.CountryCode, today))
        {
            return $"Sponsors visas for {posting.Location.CountryCode}, which you would need";
        }

        if (posting.WorkArrangement.IsRemote)
        {
            return "Fully remote, so you could do it from where you are";
        }

        if (context.PreferredCountries.Contains(
            posting.Location.CountryCode, StringComparer.Ordinal))
        {
            return $"In {posting.Location.DisplayName}, which you said you'd work in";
        }

        return $"Open in {posting.Location.DisplayName}";
    }
}
