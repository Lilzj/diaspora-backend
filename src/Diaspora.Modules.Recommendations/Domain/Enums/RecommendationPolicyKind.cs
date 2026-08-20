namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// The family a recommendation policy belongs to.
/// <para>
/// <b>A policy is not a strategy, and keeping them apart is one of this phase's structural
/// claims.</b> A strategy answers "how good is this candidate for this member" and returns a number;
/// a policy answers "may this candidate be shown to this member at all" and returns yes or no. A
/// weight of zero is not the same as an exclusion: a zero-weighted candidate still appears when
/// nothing else does, and blocked content must never appear no matter how empty the page is.
/// </para>
/// <para>
/// The consequence is enforced structurally — policies run before ranking, on the candidate pool,
/// and an architecture test asserts that no policy type implements <c>IRankingStrategy</c> and no
/// strategy type implements <c>IRecommendationPolicyRule</c>.
/// </para>
/// </summary>
public enum RecommendationPolicyKind
{
    /// <summary>Content the member blocked, or whose author they blocked.</summary>
    BlockedContent = 0,

    /// <summary>A category the member muted.</summary>
    MutedCategory = 1,

    /// <summary>A candidate whose visibility requires a trust standing the member does not have.</summary>
    TrustRestriction = 2,

    /// <summary>A candidate that is not visible in the member's country.</summary>
    CountryVisibility = 3,

    /// <summary>The member turned personalization off, or opted a surface out.</summary>
    Privacy = 4,

    /// <summary>The member has not finished onboarding, and this candidate assumes they have.</summary>
    OnboardingState = 5,

    /// <summary>The candidate requires an access tier above the member's.</summary>
    AccessTier = 6,

    /// <summary>The member already refused this target inside the suppression window.</summary>
    Dismissal = 7,

    /// <summary>The member wrote it. Recommending somebody their own listing is the oldest bug in the genre.</summary>
    SelfAuthored = 8,

    /// <summary>The candidate is no longer active — closed, sold, occupied, archived, expired.</summary>
    Availability = 9,

    /// <summary>A rule a deployment added that this module ships no implementation for.</summary>
    Custom = 100
}
