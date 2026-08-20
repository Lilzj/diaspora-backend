using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// The counters a provider accumulates.
/// <para>
/// Denormalised onto the aggregate rather than computed, because every one of these appears on
/// a card in a list — the client's discovery tab renders review counts on every row — and a
/// correlated subquery per row is the query that works in development and falls over at ten
/// thousand providers.
/// </para>
/// <para>
/// <b>Immutable, with every mutation returning a new instance.</b> A counter that can be
/// incremented in place is a counter that gets incremented from four places and reset from a
/// fifth. The aggregate holds the only reference and replaces it wholesale, which also means EF
/// sees exactly one changed owned reference and writes exactly the columns that moved.
/// </para>
/// <para>
/// <see cref="FollowerCount"/> and <see cref="PostCount"/> are <b>projections of Community</b>,
/// not B7's own numbers. B6 owns follows and posts; these are kept here so a provider card does
/// not have to call B6 per row, and they are updated from B6's integration events. A stale
/// value here can only be stale by the delivery latency of an event that has already happened.
/// </para>
/// </summary>
public sealed class ProviderStatistics : ValueObject
{
    private ProviderStatistics(
        long viewCount,
        long contactCount,
        long searchImpressionCount,
        long bookmarkCount,
        long shareCount,
        long followerCount,
        long postCount,
        int serviceCount,
        int memberCount,
        int reviewCount,
        int recommendationCount,
        int profileCompletionPercent)
    {
        ViewCount = viewCount;
        ContactCount = contactCount;
        SearchImpressionCount = searchImpressionCount;
        BookmarkCount = bookmarkCount;
        ShareCount = shareCount;
        FollowerCount = followerCount;
        PostCount = postCount;
        ServiceCount = serviceCount;
        MemberCount = memberCount;
        ReviewCount = reviewCount;
        RecommendationCount = recommendationCount;
        ProfileCompletionPercent = profileCompletionPercent;
    }

    private ProviderStatistics() { }

    /// <summary>Profile views. The client's <c>viewCount</c>.</summary>
    public long ViewCount { get; private set; }

    /// <summary>Calls, messages and directions taps. The number an owner actually cares about.</summary>
    public long ContactCount { get; private set; }

    /// <summary>Times this provider appeared in a result set. The denominator for a click rate.</summary>
    public long SearchImpressionCount { get; private set; }

    /// <summary>Saves. Projected from Community's bookmark events.</summary>
    public long BookmarkCount { get; private set; }

    public long ShareCount { get; private set; }

    /// <summary>Projected from Community. The client's <c>followersCount</c>.</summary>
    public long FollowerCount { get; private set; }

    /// <summary>Projected from Community. The client's <c>postsCount</c>.</summary>
    public long PostCount { get; private set; }

    public int ServiceCount { get; private set; }

    /// <summary>The client's <c>membersCount</c> — owners, managers, moderators, editors.</summary>
    public int MemberCount { get; private set; }

    public int ReviewCount { get; private set; }

    public int RecommendationCount { get; private set; }

    /// <summary>
    /// How complete the profile is, 0–100. Drives the onboarding nudge and is one input to the
    /// default ranking, because an incomplete profile is a worse search result regardless of
    /// how good the provider is.
    /// </summary>
    public int ProfileCompletionPercent { get; private set; }

    public static ProviderStatistics Empty =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public ProviderStatistics WithView() => With(viewCount: ViewCount + 1);

    public ProviderStatistics WithContact() => With(contactCount: ContactCount + 1);

    public ProviderStatistics WithImpressions(long count) =>
        count <= 0 ? this : With(searchImpressionCount: SearchImpressionCount + count);

    public ProviderStatistics WithBookmarkDelta(int delta) =>
        With(bookmarkCount: Floor(BookmarkCount + delta));

    public ProviderStatistics WithShareDelta(int delta) =>
        With(shareCount: Floor(ShareCount + delta));

    public ProviderStatistics WithFollowerDelta(int delta) =>
        With(followerCount: Floor(FollowerCount + delta));

    public ProviderStatistics WithPostDelta(int delta) =>
        With(postCount: Floor(PostCount + delta));

    public ProviderStatistics WithServiceCount(int count) =>
        With(serviceCount: (int)Floor(count));

    public ProviderStatistics WithMemberCount(int count) =>
        With(memberCount: (int)Floor(count));

    public ProviderStatistics WithReviewCount(int count) =>
        With(reviewCount: (int)Floor(count));

    public ProviderStatistics WithRecommendationCount(int count) =>
        With(recommendationCount: (int)Floor(count));

    public ProviderStatistics WithProfileCompletion(int percent) =>
        With(profileCompletionPercent: Math.Clamp(percent, 0, 100));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ViewCount;
        yield return ContactCount;
        yield return SearchImpressionCount;
        yield return BookmarkCount;
        yield return ShareCount;
        yield return FollowerCount;
        yield return PostCount;
        yield return ServiceCount;
        yield return MemberCount;
        yield return ReviewCount;
        yield return RecommendationCount;
        yield return ProfileCompletionPercent;
    }

    /// <summary>
    /// Counters never go below zero. A projection fed by at-least-once events will occasionally
    /// see a removal it never saw the addition for — after a replay, or a partial outage — and a
    /// provider showing "-1 followers" is a bug report that costs more than the lost precision.
    /// </summary>
    private static long Floor(long value) => value < 0 ? 0 : value;

    private ProviderStatistics With(
        long? viewCount = null,
        long? contactCount = null,
        long? searchImpressionCount = null,
        long? bookmarkCount = null,
        long? shareCount = null,
        long? followerCount = null,
        long? postCount = null,
        int? serviceCount = null,
        int? memberCount = null,
        int? reviewCount = null,
        int? recommendationCount = null,
        int? profileCompletionPercent = null) =>
        new(
            viewCount ?? ViewCount,
            contactCount ?? ContactCount,
            searchImpressionCount ?? SearchImpressionCount,
            bookmarkCount ?? BookmarkCount,
            shareCount ?? ShareCount,
            followerCount ?? FollowerCount,
            postCount ?? PostCount,
            serviceCount ?? ServiceCount,
            memberCount ?? MemberCount,
            reviewCount ?? ReviewCount,
            recommendationCount ?? RecommendationCount,
            profileCompletionPercent ?? ProfileCompletionPercent);
}
