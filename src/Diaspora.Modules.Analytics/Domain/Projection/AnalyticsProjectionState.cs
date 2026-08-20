using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Projection;

/// <summary>
/// Where the rollup projection has read to.
/// <para>
/// <b>The cursor is <c>ReceivedAt</c>, not <c>OccurredAt</c>, and that is the design.</b> A cursor on
/// occurrence time cannot advance past a late event without skipping it: an event that happened at
/// 10:01 and arrived after one that happened at 10:03 would be behind the watermark the moment it
/// landed, and would never be projected at all. Ingestion time is monotonic in write order, so
/// walking it finds every row exactly once however out of order the facts themselves are — and the
/// cell that row belongs to is then recomputed from scratch, which is where the lateness is
/// absorbed.
/// </para>
/// <para>
/// <b>The cursor is the instant alone, and the gap that leaves is closed by reading the boundary
/// instant whole.</b> A keyset over <c>(ReceivedAt, Id)</c> cannot be written — ordering a
/// value-converted strongly typed identifier inside a query is refused by DSP3003 — so the query is
/// strictly greater than the stored instant, and before the cursor advances the projection reads
/// every row at the boundary. That matters only when a page was cut at its limit part-way through a
/// group sharing one instant; ingestion staggers instants by a tick inside a batch so the group is
/// one writer's request at most.
/// </para>
/// <para>
/// Exactly one row exists. It is a single-row table rather than an options value because it is
/// state that survives a restart, and rather than a distributed lock because it is not mutual
/// exclusion — the lock is separate and does that job.
/// </para>
/// </summary>
public sealed class AnalyticsProjectionCheckpoint : AggregateRoot<AnalyticsCheckpointId>
{
    /// <summary>
    /// The one row's identity, fixed.
    /// <para>
    /// A constant rather than a generated key so the projection can load it without a scan and two
    /// instances racing the first run insert the same primary key — one of them loses at the
    /// database, which is the intended outcome.
    /// </para>
    /// </summary>
    public static readonly AnalyticsCheckpointId Singleton =
        AnalyticsCheckpointId.From(new Guid("b1900000-0000-0000-0000-000000000001"));

    private AnalyticsProjectionCheckpoint(AnalyticsCheckpointId id) : base(id)
    {
    }

    private AnalyticsProjectionCheckpoint() { }

    /// <summary>The ingestion instant of the last row folded in.</summary>
    public DateTimeOffset Position { get; private set; }

    /// <summary>
    /// The identity of the last row folded in.
    /// <para>
    /// <b>Diagnostic, not functional.</b> The cursor is <see cref="Position"/> alone — a keyset over
    /// the identifier cannot be written, because ordering a value-converted strongly typed identifier
    /// inside a query is refused by DSP3003. What this buys is that an operator looking at a stalled
    /// projection can find the exact row it stopped on, which is otherwise a search through an
    /// instant's worth of rows.
    /// </para>
    /// </summary>
    public Guid LastEventId { get; private set; }

    /// <summary>How many rows the projection has ever folded in. Operational, not analytical.</summary>
    public long ProcessedCount { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    /// <summary>EF concurrency token. Two instances that raced the lock must not both advance it.</summary>
    public int Revision { get; private set; }

    public static AnalyticsProjectionCheckpoint Start() =>
        new(Singleton) { Position = DateTimeOffset.MinValue };

    public void Advance(DateTimeOffset position, Guid lastEventId, int processed, DateTimeOffset at)
    {
        Position = position;
        LastEventId = lastEventId;
        ProcessedCount += Math.Max(0, processed);
        LastRunAt = at;
        Revision++;
    }

    /// <summary>
    /// Records that a run happened and found nothing. Keeps the freshness figure honest.
    /// <para>
    /// <b>There is deliberately no <c>Rewind</c>.</b> A backfill does not move this cursor: the
    /// cursor is about rows the projection has not seen, and a backfill is about cells it has
    /// already built. Rewinding would re-read every row in the window to reach the same answer the
    /// rebuild reaches directly — and on an incrementing projection it would double every count,
    /// which is the trap the method would exist to fall into.
    /// </para>
    /// </summary>
    public void Touch(DateTimeOffset at)
    {
        LastRunAt = at;
        Revision++;
    }
}

/// <summary>
/// One administrative rebuild of a bounded window.
/// <para>
/// <b>Bounded, resumable, observable and idempotent</b> — §30's four requirements, and each is a
/// field rather than a promise. The window is stored so a run cannot widen itself; the cursor is
/// stored so a killed run resumes where it stopped rather than from the start; the counts are
/// stored so an operator can see progress without reading logs; and the work is a recomputation
/// from the raw store, so running it twice produces the same rollups as running it once.
/// </para>
/// <para>
/// There is deliberately no "rebuild everything" without a window. §30: "Do not create a dangerous
/// rebuild-all endpoint exposed to normal users." The widest window this deployment accepts is a
/// configured number of days, and the request is refused above it rather than silently truncated.
/// </para>
/// </summary>
public sealed class AnalyticsBackfillRun : AggregateRoot<AnalyticsBackfillRunId>
{
    private AnalyticsBackfillRun(
        AnalyticsBackfillRunId id,
        DateOnly fromDay,
        DateOnly toDay,
        Guid requestedByUserId,
        DateTimeOffset requestedAt) : base(id)
    {
        FromDay = fromDay;
        ToDay = toDay;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        Status = AnalyticsBackfillStatus.Queued;
        NextDay = fromDay;
    }

    private AnalyticsBackfillRun() { }

    /// <summary>Inclusive.</summary>
    public DateOnly FromDay { get; private set; }

    /// <summary>Inclusive.</summary>
    public DateOnly ToDay { get; private set; }

    /// <summary>The day the next slice starts at. What makes the run resumable.</summary>
    public DateOnly NextDay { get; private set; }

    public AnalyticsBackfillStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public long CellsRebuilt { get; private set; }

    /// <summary>Verbatim from the failure. Never invented, and never a stack trace.</summary>
    public string? FailureReason { get; private set; }

    public int Revision { get; private set; }

    public static AnalyticsBackfillRun Request(
        AnalyticsBackfillRunId id,
        DateOnly fromDay,
        DateOnly toDay,
        Guid requestedByUserId,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new AnalyticsBackfillRun(id, fromDay, toDay, requestedByUserId, requestedAt);
    }

    public void Start(DateTimeOffset at)
    {
        Status = AnalyticsBackfillStatus.Running;
        StartedAt ??= at;
        Revision++;
    }

    /// <summary>Records a completed slice and moves the resume point past it.</summary>
    public void AdvanceTo(DateOnly nextDay, long cellsRebuilt)
    {
        NextDay = nextDay;
        CellsRebuilt += Math.Max(0, cellsRebuilt);
        Revision++;
    }

    public void Complete(DateTimeOffset at)
    {
        Status = AnalyticsBackfillStatus.Completed;
        CompletedAt = at;
        Revision++;
    }

    public void Fail(string reason, DateTimeOffset at)
    {
        Status = AnalyticsBackfillStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown failure." : reason.Trim();
        CompletedAt = at;
        Revision++;
    }

    public bool IsFinished =>
        Status is AnalyticsBackfillStatus.Completed
            or AnalyticsBackfillStatus.Failed
            or AnalyticsBackfillStatus.Cancelled;
}
