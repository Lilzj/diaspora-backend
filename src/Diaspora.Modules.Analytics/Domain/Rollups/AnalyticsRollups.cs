using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Rollups;

/// <summary>
/// The counts a rollup carries. One record, so the daily and the owner rollup cannot drift.
/// <para>
/// <b>Additive counts only.</b> Every field here can be summed across days to give the period's
/// figure, which is what makes a dashboard query a <c>GROUP BY</c> over an index rather than a scan
/// of the raw stream. Anything that cannot be summed — a distinct viewer count, an engagement rate —
/// is deliberately absent: summing daily uniques would count a returning reader once per day and
/// call the result "unique viewers", and averaging daily rates would weight a quiet Tuesday the same
/// as a busy Saturday. Both are computed from the raw store at query time and both say so.
/// </para>
/// </summary>
public sealed record AnalyticsCounts(
    long Views,
    long Saves,
    long Shares,
    long Comments,
    long Reactions,
    long HelpfulVotes,
    long Conversions,
    long Published,
    long FollowsGained,
    long FollowsLost,
    long Enquiries)
{
    /// <summary>
    /// A fresh set of zeroes.
    /// <para>
    /// <b>A new instance per access, never a cached singleton, and the platform has paid for the
    /// difference.</b> This is an EF owned type: handing one instance to two aggregates re-parents
    /// it, and the first owner's columns are written as NULL against a NOT NULL constraint. A
    /// <c>static readonly</c> field here would be that defect with a comment on it saying it was
    /// deliberate.
    /// </para>
    /// </summary>
    public static AnalyticsCounts Zero => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Engagements: what the engagement rate's numerator counts.
    /// <para>
    /// Saves, shares, comments and reactions. Stated once, here, so the rate served on a creator
    /// overview and the rate served on a content row cannot mean two different things — which is
    /// the failure §35 of the Phase 15 register exists to prevent. Views are not an engagement;
    /// they are the denominator. A conversion is not one either: an RSVP is an act on the event
    /// rather than a reaction to the post, and folding it in would let one popular event outrank a
    /// creator's whole back catalogue. Nor is an enquiry, which is a conversation with the owner
    /// rather than a response to what they published.
    /// </para>
    /// </summary>
    public long Engagements => Saves + Shares + Comments + Reactions;

    public AnalyticsCounts Add(AnalyticsCounts other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new AnalyticsCounts(
            Views + other.Views,
            Saves + other.Saves,
            Shares + other.Shares,
            Comments + other.Comments,
            Reactions + other.Reactions,
            HelpfulVotes + other.HelpfulVotes,
            Conversions + other.Conversions,
            Published + other.Published,
            FollowsGained + other.FollowsGained,
            FollowsLost + other.FollowsLost,
            Enquiries + other.Enquiries);
    }
}

/// <summary>
/// One day's figures for one piece of content.
/// <para>
/// <b>Recomputed from the raw store, never incremented.</b> The projection collects the cells a
/// batch of events touched and rewrites each from scratch. That single decision is what makes late
/// events, redelivered events, an out-of-order batch, a corrected definition and a full rebuild the
/// same operation — and it is why there is no <c>Increment</c> on this type. An incrementing
/// projection has to be replayed from zero to be corrected, and in practice never is.
/// </para>
/// <para>
/// <b><see cref="OwnerUserId"/> is denormalised onto the row</b> so a creator's overview is a range
/// scan on <c>(owner, day)</c>. Joining to the dimension instead would be correct and would also
/// re-attribute last March's figures the day a listing changed hands.
/// </para>
/// </summary>
public sealed class ContentDailyRollup : AggregateRoot<ContentDailyRollupId>
{
    private ContentDailyRollup(
        ContentDailyRollupId id,
        DateOnly day,
        AnalyticsContentType contentType,
        Guid contentId,
        Guid ownerUserId) : base(id)
    {
        Day = day;
        ContentType = contentType;
        ContentId = contentId;
        OwnerUserId = ownerUserId;
        Counts = AnalyticsCounts.Zero;
    }

    private ContentDailyRollup() { }

    /// <summary>The calendar day, in the deployment's reporting timezone.</summary>
    public DateOnly Day { get; private set; }

    public AnalyticsContentType ContentType { get; private set; }

    public Guid ContentId { get; private set; }

    public Guid OwnerUserId { get; private set; }

    /// <summary>The country the content was filed under, as of the events counted.</summary>
    public string? CountryCode { get; private set; }

    public AnalyticsCounts Counts { get; private set; } = AnalyticsCounts.Zero;

    /// <summary>
    /// Distinct viewers on this day. Not summable across days — see <see cref="AnalyticsCounts"/>.
    /// <para>
    /// Stored anyway because a single day's dashboard is the commonest read, and recomputing it for
    /// "today" from the raw store on every request would be the one query that scales with traffic.
    /// </para>
    /// </summary>
    public long DailyUniqueViewers { get; private set; }

    /// <summary>When this cell was last recomputed. The <c>dataAsOf</c> a response reports.</summary>
    public DateTimeOffset ComputedAt { get; private set; }

    public static ContentDailyRollup Create(
        ContentDailyRollupId id,
        DateOnly day,
        AnalyticsContentType contentType,
        Guid contentId,
        Guid ownerUserId)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new ContentDailyRollup(id, day, contentType, contentId, ownerUserId);
    }

    /// <summary>Replaces the cell's figures with a freshly computed set.</summary>
    public void Replace(
        AnalyticsCounts counts,
        long dailyUniqueViewers,
        Guid ownerUserId,
        string? countryCode,
        DateTimeOffset computedAt)
    {
        ArgumentNullException.ThrowIfNull(counts);

        Counts = counts;
        DailyUniqueViewers = Math.Max(0, dailyUniqueViewers);

        if (ownerUserId != Guid.Empty)
        {
            OwnerUserId = ownerUserId;
        }

        CountryCode = string.IsNullOrWhiteSpace(countryCode)
            ? CountryCode
            : countryCode.Trim().ToUpperInvariant();

        ComputedAt = computedAt;
    }
}

/// <summary>
/// One day's figures for one owner, across everything they published.
/// <para>
/// <b>Not derivable from the content rollups, which is why it exists.</b> Two of its figures have no
/// content to attach to: a follow names a member rather than an item, and a profile view is about
/// the person. Summing the content rollups would silently omit both, and a creator's follower count
/// would be permanently zero with nothing to indicate it.
/// </para>
/// </summary>
public sealed class OwnerDailyRollup : AggregateRoot<OwnerDailyRollupId>
{
    private OwnerDailyRollup(OwnerDailyRollupId id, DateOnly day, Guid ownerUserId) : base(id)
    {
        Day = day;
        OwnerUserId = ownerUserId;
        Counts = AnalyticsCounts.Zero;
    }

    private OwnerDailyRollup() { }

    public DateOnly Day { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public AnalyticsCounts Counts { get; private set; } = AnalyticsCounts.Zero;

    /// <summary>Distinct viewers of this owner's content on this day. Not summable across days.</summary>
    public long DailyUniqueViewers { get; private set; }

    /// <summary>People who opened this owner's profile on this day.</summary>
    public long ProfileViews { get; private set; }

    public DateTimeOffset ComputedAt { get; private set; }

    public static OwnerDailyRollup Create(OwnerDailyRollupId id, DateOnly day, Guid ownerUserId)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new OwnerDailyRollup(id, day, ownerUserId);
    }

    public void Replace(
        AnalyticsCounts counts,
        long dailyUniqueViewers,
        long profileViews,
        DateTimeOffset computedAt)
    {
        ArgumentNullException.ThrowIfNull(counts);

        Counts = counts;
        DailyUniqueViewers = Math.Max(0, dailyUniqueViewers);
        ProfileViews = Math.Max(0, profileViews);
        ComputedAt = computedAt;
    }
}
