using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Explainability;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Explainability;

/// <summary>
/// Chooses which of several true reasons to lead with.
/// <para>
/// <b>The largest contribution is usually not the best explanation, and this class exists because of
/// that gap.</b> A guide that scored highest on popularity, second on location and third on interest
/// is honestly described as "popular in your neighbourhood" — the location is what makes the
/// popularity relevant to this member, and "popular" alone reads as "we showed you the most-clicked
/// thing", which is both less useful and, as an account of why <em>you</em> are seeing it, less true.
/// </para>
/// <para>
/// <b>The preference order is by how personal a reason is, not by how much it contributed.</b>
/// Something the member stated beats something they did, which beats where they are, which beats
/// what everybody else did, which beats a bare quality claim. That ordering is the whole editorial
/// content of the class, and it is why this is a replaceable seam.
/// </para>
/// <para>
/// <b>Confidence is derived from corroboration, never from the score.</b> One strategy at 0.9 is a
/// low-confidence guess; four strategies agreeing at 0.5 each is a high-confidence match. Deriving
/// it from the score would make the cold-start path — where popularity alone can produce a very high
/// number — the most confident path in the module, which is exactly backwards.
/// </para>
/// </summary>
public sealed class DefaultRecommendationReasonBuilder : IRecommendationReasonBuilder
{
    /// <summary>
    /// How personal each reason is, most personal first. See the class summary.
    /// </summary>
    private static readonly RecommendationReasonCode[] Preference =
    [
        // Something the member said.
        RecommendationReasonCode.MatchesCategory,
        RecommendationReasonCode.MatchesSkills,
        RecommendationReasonCode.SearchedFor,
        RecommendationReasonCode.MovingToCountry,

        // Something the member did.
        RecommendationReasonCode.ContinueWhereYouLeftOff,
        RecommendationReasonCode.AppliedToSimilar,
        RecommendationReasonCode.SavedSimilar,
        RecommendationReasonCode.ViewedSimilar,
        RecommendationReasonCode.CompletedGuides,
        RecommendationReasonCode.AttendedEvent,
        RecommendationReasonCode.MemberOfOrganization,
        RecommendationReasonCode.JoinedCommunity,
        RecommendationReasonCode.FollowedAuthor,
        RecommendationReasonCode.TransactedInCategory,
        RecommendationReasonCode.RecentlyViewed,

        // Where the member is.
        RecommendationReasonCode.LivesInNeighborhood,
        RecommendationReasonCode.LivesInCity,
        RecommendationReasonCode.LivesInCountry,
        RecommendationReasonCode.NearYou,
        RecommendationReasonCode.SpeaksLanguage,

        // What everybody else did.
        RecommendationReasonCode.PopularInNeighborhood,
        RecommendationReasonCode.PopularInCity,
        RecommendationReasonCode.PopularInCountry,
        RecommendationReasonCode.TrendingAmongVerified,
        RecommendationReasonCode.TrendingNow,

        // What the platform can vouch for.
        RecommendationReasonCode.VerifiedProvider,
        RecommendationReasonCode.HighlyRated,
        RecommendationReasonCode.RecentlyUpdated,
        RecommendationReasonCode.RecentlyPublished,

        // Nothing better to say.
        RecommendationReasonCode.StartHere,
        RecommendationReasonCode.PopularWithNewArrivals,
        RecommendationReasonCode.NewToPlatform,
        RecommendationReasonCode.MatchesInterests
    ];

    /// <summary>Below this a contribution is too small to be worth naming as the reason.</summary>
    private const decimal MeaningfulContribution = 0.05m;

    public RecommendationExplanation Build(
        RecommendationCandidate candidate,
        IReadOnlyList<StrategyContribution> contributions,
        IReadOnlyDictionary<string, RankingScore> scoresByStrategy,
        PersonalizationContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(contributions);
        ArgumentNullException.ThrowIfNull(scoresByStrategy);
        ArgumentNullException.ThrowIfNull(context);

        var meaningful = contributions
            .Where(contribution => contribution.WeightedScore >= MeaningfulContribution)
            .Select(contribution => scoresByStrategy.GetValueOrDefault(contribution.StrategyKey))
            .Where(score => score is not null && score.ReasonCode != RecommendationReasonCode.Unspecified)
            .Select(score => score!)
            .ToList();

        var chosen = Choose(meaningful);

        if (chosen is null)
        {
            // Nothing scored high enough to name. The honest answer for a member the platform knows
            // nothing about is that this is a starting point, not that it matches them.
            var fallbackCode = context.IsColdStart
                ? RecommendationReasonCode.PopularWithNewArrivals
                : RecommendationReasonCode.MatchesInterests;

            return RecommendationExplanation.Create(
                fallbackCode,
                RecommendationReasonCatalog.ResourceKeyFor(fallbackCode),
                RecommendationReasonCatalog.TextFor(fallbackCode, null),
                RecommendationConfidence.Low);
        }

        var signalKinds = meaningful.SelectMany(score => score.SignalKinds).Distinct().ToList();

        return RecommendationExplanation.Create(
            chosen.ReasonCode,
            RecommendationReasonCatalog.ResourceKeyFor(chosen.ReasonCode),
            RecommendationReasonCatalog.TextFor(chosen.ReasonCode, chosen.ReasonDetail),
            DetermineConfidence(meaningful.Count, signalKinds.Count, context),
            signalKinds);
    }

    /// <summary>
    /// The most personal reason among those that contributed meaningfully.
    /// <para>
    /// Falls back to the largest contribution when none of the reasons appear in the preference
    /// list, which is the case a future adapter contributing an unfamiliar code lands in.
    /// </para>
    /// </summary>
    private static RankingScore? Choose(List<RankingScore> scores)
    {
        if (scores.Count == 0)
        {
            return null;
        }

        foreach (var code in Preference)
        {
            var match = scores.FirstOrDefault(score => score.ReasonCode == code);

            if (match is not null)
            {
                return match;
            }
        }

        return scores.MaxBy(score => score.Value);
    }

    /// <summary>
    /// How much the platform believes this.
    /// <para>
    /// Corroboration and breadth, never the score. See the class summary — and note that a cold-start
    /// recommendation is capped at low however many strategies agreed, because agreement between
    /// strategies that all ran on an empty profile is not corroboration.
    /// </para>
    /// </summary>
    private static RecommendationConfidence DetermineConfidence(
        int contributingStrategies, int distinctSignalKinds, PersonalizationContext context)
    {
        if (context.IsColdStart || !context.IsSurfacePersonalized)
        {
            return RecommendationConfidence.Low;
        }

        if (contributingStrategies >= 3 && distinctSignalKinds >= 2 &&
            context.Profile.Completeness >= 50)
        {
            return RecommendationConfidence.High;
        }

        return contributingStrategies >= 2 || distinctSignalKinds >= 1
            ? RecommendationConfidence.Medium
            : RecommendationConfidence.Low;
    }
}
