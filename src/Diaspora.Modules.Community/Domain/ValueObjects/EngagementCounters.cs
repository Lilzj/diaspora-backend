using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// The denormalised engagement totals a feed card renders.
/// <para>
/// <b>Denormalised counters are a deliberate trade, and the alternative is worse.</b> Counting
/// comments, reactions, shares and bookmarks live means four aggregate queries per card, twenty
/// cards per feed page, eighty queries to draw one screen. Every social platform that has tried
/// it has ended up here.
/// </para>
/// <para>
/// What makes it safe rather than merely fast is that <b>the counters are never the source of
/// truth</b>. Each one has a table behind it that can recompute it, the recount is exposed as a
/// maintenance operation, and no invariant is enforced against a counter — a comment is not
/// refused because <c>CommentCount</c> says the thread is full. A drifted counter is then a
/// cosmetic defect rather than a correctness one, which is the only version of this trade worth
/// making.
/// </para>
/// <para>
/// Every mutator clamps at zero. A decrement racing a recount can otherwise produce a negative
/// count, and a card reading "-1 comments" is the kind of thing that survives to production
/// because no test thought to assert it could not happen.
/// </para>
/// </summary>
public sealed class EngagementCounters : ValueObject
{
    private EngagementCounters(
        int viewCount,
        int commentCount,
        int reactionCount,
        int shareCount,
        int bookmarkCount,
        int reportCount)
    {
        ViewCount = viewCount;
        CommentCount = commentCount;
        ReactionCount = reactionCount;
        ShareCount = shareCount;
        BookmarkCount = bookmarkCount;
        ReportCount = reportCount;
    }

    private EngagementCounters() { }

    public int ViewCount { get; private set; }

    /// <summary>Every comment in the thread, at every depth. Not just top-level replies.</summary>
    public int CommentCount { get; private set; }

    /// <summary>Reactions of every kind. The per-kind breakdown lives in the reaction tallies.</summary>
    public int ReactionCount { get; private set; }

    public int ShareCount { get; private set; }

    public int BookmarkCount { get; private set; }

    /// <summary>
    /// Open reports. Visible to staff and to the author, never in a public projection — a
    /// report count on a public card is a brigading tool.
    /// </summary>
    public int ReportCount { get; private set; }

    public static EngagementCounters Zero => new(0, 0, 0, 0, 0, 0);

    public EngagementCounters WithViews(int delta) =>
        With(viewCount: Clamp(ViewCount + delta));

    public EngagementCounters WithComments(int delta) =>
        With(commentCount: Clamp(CommentCount + delta));

    public EngagementCounters WithReactions(int delta) =>
        With(reactionCount: Clamp(ReactionCount + delta));

    public EngagementCounters WithShares(int delta) =>
        With(shareCount: Clamp(ShareCount + delta));

    public EngagementCounters WithBookmarks(int delta) =>
        With(bookmarkCount: Clamp(BookmarkCount + delta));

    public EngagementCounters WithReports(int delta) =>
        With(reportCount: Clamp(ReportCount + delta));

    /// <summary>Replaces every counter with a recomputed value. The repair path.</summary>
    public static EngagementCounters Recomputed(
        int viewCount,
        int commentCount,
        int reactionCount,
        int shareCount,
        int bookmarkCount,
        int reportCount) =>
        new(
            Clamp(viewCount),
            Clamp(commentCount),
            Clamp(reactionCount),
            Clamp(shareCount),
            Clamp(bookmarkCount),
            Clamp(reportCount));

    private EngagementCounters With(
        int? viewCount = null,
        int? commentCount = null,
        int? reactionCount = null,
        int? shareCount = null,
        int? bookmarkCount = null,
        int? reportCount = null) =>
        new(
            viewCount ?? ViewCount,
            commentCount ?? CommentCount,
            reactionCount ?? ReactionCount,
            shareCount ?? ShareCount,
            bookmarkCount ?? BookmarkCount,
            reportCount ?? ReportCount);

    private static int Clamp(int value) => value < 0 ? 0 : value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ViewCount;
        yield return CommentCount;
        yield return ReactionCount;
        yield return ShareCount;
        yield return BookmarkCount;
        yield return ReportCount;
    }
}
