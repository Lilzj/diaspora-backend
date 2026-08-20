using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// How well a candidate matches what the member is interested in.
/// <para>
/// <b>The strategy that turns a profile into a page, and the only one that can genuinely say
/// "because you".</b> Everything else here is a property of the candidate; this is the one that
/// reads the member.
/// </para>
/// <para>
/// <b>Stated interests outrank derived ones, and by a wide margin.</b> A member who ticked
/// "immigration" during onboarding said something; a member whose affinity for immigration was
/// inferred from four page views was observed. When both point at the same candidate the explanation
/// should say the first, because it is the one the member will recognise as true.
/// </para>
/// <para>
/// <b>The strongest single match wins rather than the matches being summed.</b> Summing rewards a
/// candidate for carrying many weak tags over one that carries the exact category a member cares
/// about most — and produces explanations like "because you're interested in six things", which is
/// not a reason anybody finds convincing.
/// </para>
/// <para>
/// Returns zero and nothing else for a member with no interests. It does not run on the cold-start
/// path for exactly that reason: a strategy that contributes zero to every candidate but appears in
/// the explanation makes the explanation misleading.
/// </para>
/// </summary>
public sealed class InterestMatchRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "interest-match";

    /// <summary>A match on something the member stated is worth this much on its own.</summary>
    private const decimal ExplicitFloor = 0.85m;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.InterestMatch;

    public bool SupportsColdStart => false;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var personalization = context.Personalization;
        var scores = new List<RankingScore>(context.Candidates.Count);

        if (!personalization.IsSurfacePersonalized)
        {
            // The member turned personalization off. Returning zeroes rather than skipping keeps the
            // pass total, and the privacy policy has already made sure nothing else leaks through.
            for (var index = 0; index < context.Candidates.Count; index++)
            {
                scores.Add(RankingScore.None());
            }

            return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
        }

        var profile = personalization.Profile;
        var preference = personalization.Preference;

        foreach (var candidate in context.Candidates)
        {
            var best = BestMatch(candidate, profile);

            // A category the member explicitly asked for more of, from the preference record rather
            // than the profile. Stated beats derived — see the class summary.
            if (candidate.CategoryKey is not null &&
                preference?.IsCategoryPreferred(candidate.CategoryKey, candidate.Target.TargetType) == true)
            {
                best = (Math.Max(best.Value, ExplicitFloor),
                    RecommendationReasonCode.MatchesCategory,
                    candidate.CategoryKey,
                    true);
            }

            if (best.Value <= 0m)
            {
                scores.Add(RankingScore.None());
                continue;
            }

            scores.Add(new RankingScore
            {
                Value = decimal.Round(best.Value, 6),
                ReasonCode = best.Reason,
                ReasonDetail = best.Detail,
                SignalKinds = best.IsExplicit
                    ? [SignalKind.Interest, SignalKind.Preference]
                    : [SignalKind.Viewed, SignalKind.Saved, SignalKind.Applied]
            });
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }

    /// <summary>
    /// The single strongest interest this candidate touches.
    /// <para>
    /// Walks the candidate's attributes and its category, takes the best affinity, and lifts it to
    /// the explicit floor when the interest behind it was stated. See the class summary for why the
    /// best match wins rather than the sum.
    /// </para>
    /// </summary>
    private static (decimal Value, RecommendationReasonCode Reason, string? Detail, bool IsExplicit) BestMatch(
        RecommendationCandidate candidate, PersonalizationProfile profile)
    {
        var best = 0m;
        var reason = RecommendationReasonCode.MatchesInterests;
        string? detail = null;
        var isExplicit = false;

        if (candidate.CategoryKey is not null)
        {
            var affinity = profile.AffinityFor(InterestKind.Category, candidate.CategoryKey);

            if (affinity > best)
            {
                best = affinity;
                reason = RecommendationReasonCode.MatchesCategory;
                detail = candidate.CategoryKey;
                isExplicit = IsExplicitInterest(profile, InterestKind.Category, candidate.CategoryKey);
            }
        }

        foreach (var attribute in candidate.Attributes)
        {
            var affinity = profile.AffinityFor(attribute.Kind, attribute.Key);

            if (affinity <= best)
            {
                continue;
            }

            best = affinity;
            detail = attribute.Key;
            isExplicit = IsExplicitInterest(profile, attribute.Kind, attribute.Key);

            reason = attribute.Kind switch
            {
                InterestKind.Skill => RecommendationReasonCode.MatchesSkills,
                InterestKind.Category => RecommendationReasonCode.MatchesCategory,
                InterestKind.SearchTerm => RecommendationReasonCode.SearchedFor,
                InterestKind.Language => RecommendationReasonCode.SpeaksLanguage,
                _ => RecommendationReasonCode.MatchesInterests
            };
        }

        if (isExplicit && best < ExplicitFloor)
        {
            best = ExplicitFloor;
        }

        return (best, reason, detail, isExplicit);
    }

    private static bool IsExplicitInterest(PersonalizationProfile profile, InterestKind kind, string key) =>
        profile.Interests.Any(interest =>
            interest.IsExplicit &&
            interest.Kind == kind &&
            string.Equals(interest.Key, key.Trim().ToLowerInvariant(), StringComparison.Ordinal));
}
