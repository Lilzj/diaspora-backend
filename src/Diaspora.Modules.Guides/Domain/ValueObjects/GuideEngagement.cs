using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// The denormalised engagement counters a guide card renders.
/// <para>
/// A copy of what other aggregates know — votes live in <c>GuideVote</c> rows, bookmarks in
/// Community, comments in Community — kept here so a discovery page of twenty cards is one
/// query. Deltas are clamped at zero rather than trusted: a decrement that arrives twice (at-
/// least-once delivery) must not drive a public number negative, and a card showing "-1
/// bookmarks" is worse than a card showing a count that is briefly one off.
/// </para>
/// <para>
/// Mutating a counter is <em>not</em> editing the guide: none of these methods appears in a
/// path that bumps the revision or raises an update event.
/// </para>
/// </summary>
public sealed class GuideEngagement : ValueObject
{
    private GuideEngagement(
        long views,
        long uniqueViews,
        int helpfulCount,
        int notHelpfulCount,
        int bookmarkCount,
        int shareCount,
        int commentCount)
    {
        Views = views;
        UniqueViews = uniqueViews;
        HelpfulCount = helpfulCount;
        NotHelpfulCount = notHelpfulCount;
        BookmarkCount = bookmarkCount;
        ShareCount = shareCount;
        CommentCount = commentCount;
    }

    private GuideEngagement() { }

    /// <summary>All zeroes. What every guide starts with.</summary>
    public static GuideEngagement None => new(0, 0, 0, 0, 0, 0, 0);

    public long Views { get; private set; }

    public long UniqueViews { get; private set; }

    public int HelpfulCount { get; private set; }

    public int NotHelpfulCount { get; private set; }

    public int BookmarkCount { get; private set; }

    public int ShareCount { get; private set; }

    public int CommentCount { get; private set; }

    public GuideEngagement WithView(bool unique) => new(
        Views + 1,
        unique ? UniqueViews + 1 : UniqueViews,
        HelpfulCount,
        NotHelpfulCount,
        BookmarkCount,
        ShareCount,
        CommentCount);

    public GuideEngagement WithVoteDelta(int helpfulDelta, int notHelpfulDelta) => new(
        Views,
        UniqueViews,
        Math.Max(0, HelpfulCount + helpfulDelta),
        Math.Max(0, NotHelpfulCount + notHelpfulDelta),
        BookmarkCount,
        ShareCount,
        CommentCount);

    public GuideEngagement WithBookmarkDelta(int delta) => new(
        Views,
        UniqueViews,
        HelpfulCount,
        NotHelpfulCount,
        Math.Max(0, BookmarkCount + delta),
        ShareCount,
        CommentCount);

    public GuideEngagement WithShare() => new(
        Views,
        UniqueViews,
        HelpfulCount,
        NotHelpfulCount,
        BookmarkCount,
        ShareCount + 1,
        CommentCount);

    /// <summary>Replaced, not incremented — Community owns the saves and is recounted, not trusted to delta.</summary>
    public GuideEngagement WithBookmarkCount(int count) => new(
        Views,
        UniqueViews,
        HelpfulCount,
        NotHelpfulCount,
        Math.Max(0, count),
        ShareCount,
        CommentCount);

    /// <summary>Replaced, not incremented — Community owns the count and is recounted, not trusted to delta.</summary>
    public GuideEngagement WithCommentCount(int count) => new(
        Views,
        UniqueViews,
        HelpfulCount,
        NotHelpfulCount,
        BookmarkCount,
        ShareCount,
        Math.Max(0, count));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Views;
        yield return UniqueViews;
        yield return HelpfulCount;
        yield return NotHelpfulCount;
        yield return BookmarkCount;
        yield return ShareCount;
        yield return CommentCount;
    }
}
