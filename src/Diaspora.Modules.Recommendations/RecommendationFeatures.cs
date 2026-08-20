namespace Diaspora.Modules.Recommendations;

/// <summary>
/// The feature gates the Recommendation Platform owns.
/// <para>
/// <b>The feed is gated at the lowest tier the platform grants anything at, and that is a
/// deliberate choice rather than an oversight.</b> A member halfway through onboarding is exactly
/// the member with no history, in a country they have just arrived in, who most needs to be told
/// which guide to read first. Gating the feed above <c>Onboarding</c> would mean the cold-start
/// path — the whole reason this module handles a member with no signals at all — could never run
/// for the members it was written for.
/// </para>
/// <para>
/// What <em>is</em> gated is everything that changes what other people see: authoring a ranking
/// strategy, authoring a recommendation policy and reading analytics across members. A ranking
/// weight is a decision about what hundreds of thousands of people are shown.
/// </para>
/// <para>
/// <b>The first gate is called <c>recommendations.view</c> and not <c>recommendations.feed</c>, and
/// the rename was the platform correcting this module rather than the other way round.</b> B2.0.1's
/// <c>No_onboarding_capability_is_a_membership_privilege</c> fires on a capability's <em>name</em>,
/// before anybody has to reason about intent, and "feed" is on its vocabulary because reading the
/// community feed is a membership privilege that must never open before onboarding. This module's
/// feed is a different thing — a set of suggestions addressed to exactly one person, reaching
/// nobody else — but the guard cannot know that, and weakening its vocabulary to admit this one case
/// would disarm it for the case it was written for. Not claiming the name is much cheaper than
/// arguing about it.
/// </para>
/// </summary>
public static class RecommendationFeatures
{
    /// <summary>
    /// See your own recommendations. Open at the lowest tier on purpose — see the class summary.
    /// <para>
    /// Named <c>view</c> rather than <c>feed</c> for the reason the class summary gives.
    /// </para>
    /// </summary>
    public const string View = "recommendations.view";

    /// <summary>Read and change your own personalization profile and preferences.</summary>
    public const string Personalization = "recommendations.personalization";

    /// <summary>Record interaction signals from the client.</summary>
    public const string Signals = "recommendations.signals";

    /// <summary>Author, weight and retire ranking strategies. Staff only.</summary>
    public const string Strategies = "recommendations.strategies";

    /// <summary>Author and retire recommendation policies. Staff only.</summary>
    public const string Policies = "recommendations.policies";

    /// <summary>Read recommendation analytics across members. Staff only.</summary>
    public const string Analytics = "recommendations.analytics";
}
