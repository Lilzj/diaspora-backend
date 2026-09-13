using Diaspora.Modules.Analytics.Domain.Dimensions;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Events;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Projection;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Domain.Schema;

namespace Diaspora.Modules.Analytics.Domain.Repositories;

/// <summary>
/// One cell's counts as the database computed them.
/// <para>
/// A flat record rather than <c>AnalyticsCounts</c> because it crosses the query boundary: EF
/// projects into it directly, and giving it the owned type's shape would put a computed property
/// and an <c>Add</c> method inside a projection.
/// </para>
/// </summary>
public sealed record AnalyticsCellCounts(
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
    long ProfileViews,
    long Enquiries)
{
    public static AnalyticsCellCounts Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// The raw event store, as the domain sees it.
/// <para>
/// <b>Append and bounded read, with no update.</b> The immutability of <see cref="AnalyticsEvent"/>
/// expressed as a port rather than as a comment: a repository with no <c>Update</c> cannot be used
/// to edit history, whatever a future handler intends. The one exception is
/// <see cref="ListUnattributedAsync"/>, which feeds late owner attribution — a join key, never a
/// fact.
/// </para>
/// <para>
/// <b>The two deletion methods are named for their reasons.</b> <see cref="DeleteExpiredAsync"/> is
/// retention; <see cref="DeleteForActorAsync"/> is erasure. They do the same thing to different rows
/// and conflating them would mean an erasure request that honoured the retention window instead of
/// the request.
/// </para>
/// </summary>
public interface IAnalyticsEventRepository
{
    void Add(AnalyticsEvent analyticsEvent);

    /// <summary>
    /// Which of these deduplication keys are already stored.
    /// <para>
    /// A set read rather than a per-event existence check, because a telemetry batch is up to fifty
    /// events and fifty round trips per request is the shape of an ingestion endpoint that falls
    /// over under the load it exists to absorb. The unique index behind it is still the barrier —
    /// this only avoids paying for the round trip when the answer is knowable in one.
    /// </para>
    /// </summary>
    Task<IReadOnlyCollection<string>> FindExistingKeysAsync(
        IReadOnlyCollection<string> dedupKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// The next slice of the stream, in ingestion order, strictly after the cursor.
    /// <para>
    /// <b>Strictly after, and ordered by ingestion time alone.</b> A keyset over
    /// <c>(ReceivedAt, Id)</c> cannot be written here: ordering a value-converted strongly typed
    /// identifier inside a query is refused by DSP3003 and does not translate on every provider. So
    /// the cursor is the instant alone, and the caller closes the gap that leaves — see
    /// <see cref="ListAtAsync"/>.
    /// </para>
    /// <para>
    /// <b>An inclusive cursor was the first implementation and it was wrong in a way that never
    /// failed a test.</b> Re-reading the boundary row is harmless to the figures, because the
    /// projection recomputes rather than increments — but it means the batch is never empty, so the
    /// projection never idles: every tick, forever, it re-reads one row and recomputes its cells on
    /// a platform with no traffic. Caught by reading the loop rather than by a red test.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AnalyticsEvent>> ListAfterAsync(
        DateTimeOffset position, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every row at exactly one ingestion instant.
    /// <para>
    /// <b>What makes a strictly-greater cursor safe.</b> A page cut at its limit may have stopped
    /// halfway through a group of rows sharing one instant; advancing past that instant would skip
    /// the rest of the group permanently. Before advancing, the projection reads the boundary
    /// instant whole. In practice a tie group is one ingestion request's worth of rows — ingestion
    /// staggers instants by a tick inside a batch precisely so it stays that way — and this exists
    /// because "in practice" is not a guarantee under two writers.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AnalyticsEvent>> ListAtAsync(
        DateTimeOffset instant, CancellationToken cancellationToken = default);

    /// <summary>
    /// One cell's counts, computed by the database.
    /// <para>
    /// <b>Aggregated in SQL rather than by loading the cell's rows.</b> A popular item can collect
    /// a hundred thousand views in a day, and a projection that materialised them to count them
    /// would be the client-side aggregation §25 forbids — inside the component whose job is to make
    /// it unnecessary. One grouped query with conditional sums answers all of it.
    /// </para>
    /// </summary>
    Task<AnalyticsCellCounts> AggregateContentDayAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default);

    /// <summary>One owner's counts for a day, across everything attributed to them.</summary>
    Task<AnalyticsCellCounts> AggregateOwnerDayAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>Distinct viewers of one item on one day.</summary>
    Task<long> CountDistinctViewersForContentDayAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default);

    /// <summary>Distinct viewers of one owner's work on one day.</summary>
    Task<long> CountDistinctViewersForOwnerDayAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>The distinct (contentType, contentId, day) cells touched by a set of rows.</summary>
    Task<IReadOnlyList<(DateOnly Day, Guid OwnerUserId)>> ListOwnerCellsAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default);

    /// <summary>The distinct content cells with activity in a window. Drives the backfill.</summary>
    Task<IReadOnlyList<(DateOnly Day, AnalyticsContentType ContentType, Guid ContentId)>>
        ListContentCellsAsync(
            DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct actors over a window for one owner. The period unique-viewer figure.
    /// <para>
    /// Answered from the raw store rather than from the rollups, because distinct counts do not sum:
    /// a reader who came back on three days is one unique viewer and three daily ones, and adding
    /// the daily figures would report the second number under the first one's name.
    /// </para>
    /// </summary>
    Task<long> CountDistinctViewersForOwnerAsync(
        Guid ownerUserId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct actors over a window for one owner's content of one type.
    /// <para>
    /// <b>The owner count narrowed, and the narrowing is not optional for a seller.</b> The owner
    /// count covers everything an owner published, so a seller who also writes guides would have
    /// every guide reader counted as somebody who looked at their listings — a larger number, under
    /// a name that says it is the smaller one, and wrong in the flattering direction.
    /// </para>
    /// </summary>
    Task<long> CountDistinctViewersForOwnerContentTypeAsync(
        Guid ownerUserId,
        AnalyticsContentType contentType,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>Distinct actors over a window for one piece of content.</summary>
    Task<long> CountDistinctViewersForContentAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>Rows that name content whose owner analytics did not know at the time.</summary>
    Task<IReadOnlyList<AnalyticsEvent>> ListUnattributedAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Retention. Deletes rows whose window has closed, in bounded batches.</summary>
    Task<int> DeleteExpiredAsync(
        DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Erasure. Deletes every row this module observed about one member.
    /// <para>
    /// Takes the actor key rather than a user identifier, because the key is all this table holds —
    /// the caller hashes once and this method deletes precisely. See <see cref="AnalyticsEvent"/>
    /// for why the raw identifier is not stored.
    /// </para>
    /// </summary>
    Task<int> DeleteForActorAsync(string actorKey, CancellationToken cancellationToken = default);
}

/// <summary>The content dimension every attribution and every authorization decision joins through.</summary>
public interface IAnalyticsContentDimensionRepository
{
    void Add(AnalyticsContentDimension dimension);

    Task<AnalyticsContentDimension?> FindAsync(
        AnalyticsContentType contentType, Guid contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A batch of dimensions, for rendering a page of content-performance rows.
    /// <para>
    /// One query for a page rather than one per row. The N+1 §25 forbids is the default shape of a
    /// list that resolves a title per item, and the only reliable defence is not having a per-item
    /// lookup to call.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AnalyticsContentDimension>> ListAsync(
        IReadOnlyCollection<(AnalyticsContentType ContentType, Guid ContentId)> keys,
        CancellationToken cancellationToken = default);

    /// <summary>Everything one owner has published, for the content-performance list's universe.</summary>
    Task<IReadOnlyList<AnalyticsContentDimension>> ListForOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken = default);
}

/// <summary>The daily rollups. Written only by the projection, read by every query.</summary>
public interface IAnalyticsRollupRepository
{
    void Add(ContentDailyRollup rollup);

    void Add(OwnerDailyRollup rollup);

    Task<ContentDailyRollup?> FindContentAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default);

    Task<OwnerDailyRollup?> FindOwnerAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>One owner's daily rows across a window, oldest first. The overview and the series.</summary>
    Task<IReadOnlyList<OwnerDailyRollup>> ListOwnerAsync(
        Guid ownerUserId, DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default);

    /// <summary>Every content row for one owner across a window. The per-item breakdown.</summary>
    Task<IReadOnlyList<ContentDailyRollup>> ListContentForOwnerAsync(
        Guid ownerUserId, DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default);

    /// <summary>One item's daily rows across a window.</summary>
    Task<IReadOnlyList<ContentDailyRollup>> ListContentAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly fromDay,
        DateOnly toDay,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether analytics recorded anything for one owner on a day before <paramref name="day"/>.
    /// <para>
    /// <b>What tells a quiet window from an unrecorded one.</b> A row exists only for a day on which
    /// something happened, so a window with no rows cannot say by itself whether its subject had a
    /// quiet spell or had never been recorded. An earlier row can — and one existence check on the
    /// owner-and-day index is all it costs.
    /// </para>
    /// </summary>
    Task<bool> AnyOwnerBeforeAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether analytics recorded anything for one owner's content of one type before a day.
    /// <para>
    /// Narrowed for the reason the seller's totals are: a member's guides are not their listings, and
    /// a guide read last month is no history for a listing published today.
    /// </para>
    /// </summary>
    Task<bool> AnyOwnerContentTypeBeforeAsync(
        Guid ownerUserId,
        AnalyticsContentType contentType,
        DateOnly day,
        CancellationToken cancellationToken = default);

    /// <summary>Whether analytics recorded anything for one item before a day.</summary>
    Task<bool> AnyContentBeforeAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of one owner's items analytics recorded anything for before a day.
    /// <para>
    /// One query for a page of per-item rows rather than one existence check per row — the N+1 §25
    /// forbids, which is the default shape of a list that asks a question about each item.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<(AnalyticsContentType ContentType, Guid ContentId)>> ListContentRecordedBeforeAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>Retention. Rollups outlive raw events by design — see the retention policy.</summary>
    Task<int> DeleteExpiredAsync(
        DateOnly olderThan, int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>The projection's cursor and the backfill runs against it.</summary>
public interface IAnalyticsProjectionRepository
{
    Task<AnalyticsProjectionCheckpoint?> FindCheckpointAsync(CancellationToken cancellationToken = default);

    void Add(AnalyticsProjectionCheckpoint checkpoint);

    void Add(AnalyticsBackfillRun run);

    Task<AnalyticsBackfillRun?> FindRunAsync(
        AnalyticsBackfillRunId id, CancellationToken cancellationToken = default);

    /// <summary>The run that is queued or running, when there is one. At most one exists.</summary>
    Task<AnalyticsBackfillRun?> FindActiveRunAsync(CancellationToken cancellationToken = default);
}

/// <summary>The event schema registry. Reference data, seeded, read on every ingestion.</summary>
public interface IAnalyticsEventSchemaRepository
{
    void Add(AnalyticsEventSchema schema);

    Task<IReadOnlyList<AnalyticsEventSchema>> ListAsync(CancellationToken cancellationToken = default);

    Task<AnalyticsEventSchema?> FindAsync(
        string eventType, int version, CancellationToken cancellationToken = default);

    /// <summary>Whether the registry knows the name at any version. Distinguishes the two refusals.</summary>
    Task<bool> ExistsAsync(string eventType, CancellationToken cancellationToken = default);
}
