namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// Why a member is being shown something.
/// <para>
/// <b>A code, never a sentence, and that is the same decision the kernel made for
/// <c>FeatureBlockCode</c>.</b> This module decides <em>why</em> a recommendation was made; the
/// client decides how to say so, in the member's language. Putting "Because you live in Mauritius"
/// in the domain would make every translation a backend release, and would mean a member reading
/// the app in French getting an English explanation for the one thing on the screen that is
/// supposed to build trust.
/// </para>
/// <para>
/// The API carries the code, a stable resource key and a plain-English fallback rendering. The
/// fallback exists because a client that has not shipped a translation for a newly added code must
/// show something honest rather than a blank card or a raw enum name.
/// </para>
/// </summary>
public enum RecommendationReasonCode
{
    /// <summary>Nothing better could be said. Always accompanied by the fallback strategy.</summary>
    Unspecified = 0,

    // ── Where the member is ─────────────────────────────────────────────────────────────────

    LivesInCountry = 1,
    LivesInCity = 2,
    LivesInNeighborhood = 3,
    NearYou = 4,
    MovingToCountry = 5,

    // ── Who the member is ───────────────────────────────────────────────────────────────────

    SpeaksLanguage = 10,
    MatchesInterests = 11,
    MatchesCategory = 12,
    MatchesSkills = 13,
    NewToPlatform = 14,

    // ── What the member did ─────────────────────────────────────────────────────────────────

    RecentlyViewed = 20,
    ViewedSimilar = 21,
    SavedSimilar = 22,
    AppliedToSimilar = 23,
    SearchedFor = 24,
    JoinedCommunity = 25,
    MemberOfOrganization = 26,
    AttendedEvent = 27,
    FollowedAuthor = 28,
    CompletedGuides = 29,
    ContinueWhereYouLeftOff = 30,
    TransactedInCategory = 31,

    // ── What everybody else did ─────────────────────────────────────────────────────────────

    PopularInNeighborhood = 40,
    PopularInCity = 41,
    PopularInCountry = 42,
    TrendingAmongVerified = 43,
    TrendingNow = 44,

    // ── What the platform can vouch for ─────────────────────────────────────────────────────

    VerifiedProvider = 50,
    HighlyRated = 51,
    RecentlyPublished = 52,
    RecentlyUpdated = 53,

    // ── When there is nothing better ────────────────────────────────────────────────────────

    PopularWithNewArrivals = 60,
    StartHere = 61,

    /// <summary>
    /// A paid placement the Financial Platform activated over this subject.
    /// <para>
    /// <b>Its own code rather than reuse of <see cref="HighlyRated"/>, and that is the point of the
    /// whole integration.</b> B15 keeps financial state apart from display state so that "did this
    /// rank because it is good or because it was paid for" stays answerable; a promotion that
    /// explained itself as "highly rated" would answer it wrongly, in writing, to the member.
    /// </para>
    /// </summary>
    Promoted = 62
}
