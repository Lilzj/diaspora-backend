using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Events;

/// <summary>
/// One raw analytics fact. Written once, never changed.
/// <para>
/// <b>This is the whole of the platform's answer to "what happened, and when".</b> Everything a
/// dashboard serves is recomputed from rows of this table, which is what makes a late event, a
/// redelivered event, a corrected definition and a full rebuild the same operation rather than four
/// mechanisms. A rollup that could not be re-derived would have to be patched, and a patched
/// aggregate is one nobody can check.
/// </para>
///
/// <para><b>The three timestamps, and why none of them is redundant.</b></para>
/// <list type="bullet">
///   <item>
///     <see cref="OccurredAt"/> is when the fact happened, as its producer reported it. Every
///     analytical question is asked of this one. An event that happened at 10:01 and arrived at
///     10:05 belongs to 10:01's bucket, or the platform's trends measure its own queue depth.
///   </item>
///   <item>
///     <see cref="ReceivedAt"/> is when this module wrote it down. It is ingestion metadata, it is
///     monotonic in write order, and it is what the projection walks — so a late arrival is
///     <em>found</em> by the projection even though it lands in an old bucket.
///   </item>
///   <item>
///     <see cref="OccurredOn"/> is the calendar day <see cref="OccurredAt"/> falls in, in the
///     deployment's reporting timezone, frozen at ingestion. Storing it rather than deriving it is
///     what lets a daily rollup be a grouped query instead of a per-row timezone conversion, and
///     freezing it is what stops a reporting-timezone change silently re-dating three years of
///     history.
///   </item>
/// </list>
///
/// <para>
/// <b><see cref="DedupKey"/> is the idempotency barrier, and it is derived from what the event is
/// rather than from what the caller called it.</b> A client that chose its own event identifier
/// could send a fresh one per retry and be counted twice, or a colliding one and silently suppress
/// somebody else's row. The key is composed from the event type, the subject, the actor and — for
/// the classes whose published definition says so — the day. It is uniquely indexed, so two workers
/// racing one redelivery lose one at the database rather than both counting.
/// </para>
///
/// <para>
/// <b><see cref="ActorKey"/> is a keyed hash and never a user identifier.</b> A distinct-viewer
/// count needs to know that two views were the same person; it does not need to know which person,
/// and a table that knew would be a readership record — "there must be no per-viewer endpoint at
/// any level of authorization short of a legal process" is the register's own line. The hash is
/// deterministic, so an erasure request can compute it once and delete precisely; it is
/// pseudonymous rather than anonymous, and the retention policy says so rather than claiming
/// otherwise.
/// </para>
///
/// <para>
/// <b>Dimensions are frozen as of the event.</b> A member's country and a piece of content's
/// community are recorded as they were when the fact happened, not looked up now. Analytics must
/// not retroactively rewrite history because a profile changed, and the only way to guarantee that
/// is to have nothing to look up.
/// </para>
/// </summary>
public sealed class AnalyticsEvent : AggregateRoot<AnalyticsEventId>
{
    private AnalyticsEvent(
        AnalyticsEventId id,
        string eventType,
        int eventVersion,
        AnalyticsTrustClass trustClass,
        string sourceModule,
        string dedupKey,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        DateOnly occurredOn) : base(id)
    {
        EventType = eventType;
        EventVersion = eventVersion;
        TrustClass = trustClass;
        SourceModule = sourceModule;
        DedupKey = dedupKey;
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
        OccurredOn = occurredOn;
    }

    private AnalyticsEvent() { }

    /// <summary>A member of <c>AnalyticsEventTypes</c>, checked against the schema registry at ingestion.</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// Which version of the contract this row was written against.
    /// <para>
    /// Stored on the row rather than assumed, so a consumer reading three-month-old history knows
    /// which schema produced it. An unknown version is refused at ingestion and counted, rather
    /// than accepted and silently reinterpreted — the failure mode a schema change without a
    /// version produces is a chart that changes shape retroactively.
    /// </para>
    /// </summary>
    public int EventVersion { get; private set; } = 1;

    public AnalyticsTrustClass TrustClass { get; private set; }

    /// <summary>Which module's contract produced it, or <c>client</c>.</summary>
    public string SourceModule { get; private set; } = string.Empty;

    /// <summary>See the class summary. Uniquely indexed.</summary>
    public string DedupKey { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateOnly OccurredOn { get; private set; }

    /// <summary>What the fact was about. Null for facts that are not about a piece of content.</summary>
    public AnalyticsContentType ContentType { get; private set; } = AnalyticsContentType.Unknown;

    public Guid? ContentId { get; private set; }

    /// <summary>
    /// Whose content it was, resolved at ingestion from the content dimension.
    /// <para>
    /// Denormalised deliberately: a creator's overview is a filter on this column and a date, which
    /// is one index. Resolving the owner at query time would mean a join to a dimension that may
    /// since have changed hands, and March's figures would move.
    /// </para>
    /// </summary>
    public Guid? OwnerUserId { get; private set; }

    /// <summary>Who did it, as a keyed hash. See the class summary. Null for facts with no actor.</summary>
    public string? ActorKey { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 as of the event, upper case.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>The community the fact happened in, as of the event.</summary>
    public Guid? CommunityId { get; private set; }

    /// <summary>Where it happened — a feed, a search result, a share link. Low cardinality.</summary>
    public string? Surface { get; private set; }

    /// <summary>
    /// A low-cardinality qualifier the metric may filter on — a reaction key, a conversion kind.
    /// <para>
    /// One column rather than a JSON payload, and that is the privacy design rather than a
    /// modelling shortcut: a free-form payload is where an email address eventually ends up, and a
    /// single bounded string with an allow-list behind it has nowhere to put one.
    /// </para>
    /// </summary>
    public string? Qualifier { get; private set; }

    /// <summary>
    /// What this occurrence counts as. One, except where a producer reports a batch.
    /// <para>
    /// An RSVP for a party of four is one conversion and four attendees, and the two questions have
    /// different answers; the weight is how the second is representable without a second row.
    /// </para>
    /// </summary>
    public int Weight { get; private set; } = 1;

    /// <summary>The request that produced the fact, for tracing a figure back to a call.</summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>The published event this was derived from, where it was derived from one.</summary>
    public Guid? CausationId { get; private set; }

    /// <summary>
    /// Records a fact.
    /// <para>
    /// The only constructor. Every field the projection will read is supplied here, because there
    /// is no second call that could add one later and no mutation that could change one.
    /// </para>
    /// </summary>
    public static AnalyticsEvent Record(
        AnalyticsEventId id,
        string eventType,
        int eventVersion,
        AnalyticsTrustClass trustClass,
        string sourceModule,
        string dedupKey,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        DateOnly occurredOn,
        AnalyticsContentType contentType = AnalyticsContentType.Unknown,
        Guid? contentId = null,
        Guid? ownerUserId = null,
        string? actorKey = null,
        string? countryCode = null,
        Guid? communityId = null,
        string? surface = null,
        string? qualifier = null,
        int weight = 1,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(dedupKey);

        return new AnalyticsEvent(
            id,
            eventType.Trim(),
            Math.Max(1, eventVersion),
            trustClass,
            sourceModule.Trim(),
            dedupKey.Trim(),
            occurredAt,
            receivedAt,
            occurredOn)
        {
            ContentType = contentType,
            ContentId = contentId == Guid.Empty ? null : contentId,
            OwnerUserId = ownerUserId == Guid.Empty ? null : ownerUserId,
            ActorKey = string.IsNullOrWhiteSpace(actorKey) ? null : actorKey,
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            CommunityId = communityId == Guid.Empty ? null : communityId,
            Surface = string.IsNullOrWhiteSpace(surface) ? null : surface.Trim(),
            Qualifier = string.IsNullOrWhiteSpace(qualifier) ? null : qualifier.Trim(),
            Weight = Math.Max(1, weight),
            CorrelationId = correlationId == Guid.Empty ? null : correlationId,
            CausationId = causationId == Guid.Empty ? null : causationId
        };
    }

    /// <summary>
    /// Attributes a row to an owner that was not known when it was written.
    /// <para>
    /// <b>The one mutation this aggregate permits, and it changes no fact.</b> A view can legitimately
    /// arrive before the publication event that names the content's author — the two travel on
    /// different paths and the outbox makes no ordering promise — and a row stranded without an
    /// owner would be invisible to every creator query forever. Late attribution sets the join key
    /// and nothing else; the fact, its time and its actor are untouched, and a test asserts the row
    /// is otherwise identical afterwards.
    /// </para>
    /// </summary>
    public void AttributeTo(Guid ownerUserId, AnalyticsContentType contentType)
    {
        if (ownerUserId == Guid.Empty || OwnerUserId is not null)
        {
            return;
        }

        OwnerUserId = ownerUserId;

        if (ContentType == AnalyticsContentType.Unknown)
        {
            ContentType = contentType;
        }
    }
}
