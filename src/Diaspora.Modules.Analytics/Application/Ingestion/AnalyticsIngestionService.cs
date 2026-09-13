using System.Globalization;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Events;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Schema;
using Diaspora.Modules.Analytics.Observability;
using Diaspora.Modules.Analytics.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Ingestion;

/// <summary>
/// The one door into the raw analytics store.
/// <para>
/// <b>Everything that ends up in the event table comes through here</b> — every integration event
/// consumer and the one telemetry endpoint — so the deduplication rule, the actor hashing, the owner
/// attribution, the timestamp clamping, the trust classification and the counters are written once
/// and cannot differ between paths. A second write path is how a platform ends up with rows that
/// obey half the rules.
/// </para>
/// <para>
/// <b>It stages, and never saves.</b> A consumer's unit of work is committed by the messaging
/// pipeline and an endpoint's by its handler; a service that saved would commit a caller's other
/// pending work as a side effect, and would break the one property the telemetry endpoint depends
/// on — that a batch is one transaction, so a duplicate inside it is caught before the unique index
/// refuses the lot.
/// </para>
/// </summary>
public sealed class AnalyticsIngestionService
{
    private readonly IAnalyticsEventRepository _events;
    private readonly IAnalyticsContentDimensionRepository _dimensions;
    private readonly IAnalyticsEventSchemaRepository _schemas;
    private readonly AnalyticsActorHasher _hasher;
    private readonly AnalyticsReportingCalendar _calendar;
    private readonly AnalyticsOptions _options;

    public AnalyticsIngestionService(
        IAnalyticsEventRepository events,
        IAnalyticsContentDimensionRepository dimensions,
        IAnalyticsEventSchemaRepository schemas,
        AnalyticsActorHasher hasher,
        AnalyticsReportingCalendar calendar,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _events = events;
        _dimensions = dimensions;
        _schemas = schemas;
        _hasher = hasher;
        _calendar = calendar;
        _options = options.Value;
    }

    /// <summary>
    /// Stages a batch of facts.
    /// <para>
    /// <b>Batched rather than one at a time, and the reason is the deduplication read.</b> A batch of
    /// fifty events checked one at a time is fifty round trips on the platform's only unauthenticated-
    /// volume write path. One set read answers all fifty, and the unique index remains the barrier
    /// for the race the read cannot see.
    /// </para>
    /// <para>
    /// <paramref name="isClientSubmission"/> decides two things and nothing else: whether a schema
    /// that forbids client submission refuses the row, and whether the occurrence time is clamped
    /// against the configured window. A server contract's timestamp is the producing module's and is
    /// taken as given — clamping it would rewrite a fact the platform committed.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<AnalyticsIngestionResult>> IngestAsync(
        IReadOnlyList<AnalyticsFact> facts,
        bool isClientSubmission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var results = new AnalyticsIngestionResult[facts.Count];

        if (facts.Count == 0)
        {
            return results;
        }

        var receivedAt = _calendar.UtcNow;
        var prepared = new PreparedFact?[facts.Count];

        for (var index = 0; index < facts.Count; index++)
        {
            var fact = facts[index];

            // ── The schema lookup happens before any instrument is touched, and that ordering is a
            //    security property rather than a tidiness one — B24-G51.
            //
            // This method's `event` tag used to be `fact.EventType`, which on the client path is a
            // string the caller chose, recorded before anything had checked whether the platform had
            // ever heard of it. An authenticated caller posting a full batch of random names minted
            // two new metric time series per name, per batch, for as long as they cared to keep
            // posting — a metric-cardinality attack, which §41 names in its own test list and §12
            // forbids outright. Every metric below is now tagged with a name that came out of the
            // registry, or with the single `unregistered` bucket.
            //
            // **The refusal itself stays visible per event**: the caller is still told exactly which
            // name was not recognised, in the response body, where an unbounded string costs one
            // response rather than a permanent series.
            var schema = await _schemas
                .FindAsync(fact.EventType, fact.Version, cancellationToken)
                .ConfigureAwait(false);

            var label = schema?.EventType ?? UnregisteredEvent;

            AnalyticsTelemetry.EventsReceived.Add(
                1,
                new KeyValuePair<string, object?>("event", label),
                new KeyValuePair<string, object?>(
                    "trust", isClientSubmission ? "client" : "server"));

            if (schema is null)
            {
                var known = await _schemas
                    .ExistsAsync(fact.EventType, cancellationToken)
                    .ConfigureAwait(false);

                results[index] = Reject(
                    label,
                    known
                        ? AnalyticsErrorCodes.EventVersionUnknown
                        : AnalyticsErrorCodes.EventTypeUnknown);
                continue;
            }

            if (!schema.IsAccepted)
            {
                results[index] = Reject(label, AnalyticsErrorCodes.EventTypeUnknown);
                continue;
            }

            if (isClientSubmission && !schema.AllowsClientSubmission)
            {
                results[index] = Reject(label, AnalyticsErrorCodes.EventNotClientSubmittable);
                continue;
            }

            if (schema.RequiresContent && (fact.ContentId is null || fact.ContentId == Guid.Empty))
            {
                results[index] = Reject(label, AnalyticsErrorCodes.EventMalformed);
                continue;
            }

            var occurredAt = fact.OccurredAt;

            if (isClientSubmission)
            {
                var clamped = ClampClientTime(occurredAt, receivedAt);

                if (clamped is null)
                {
                    results[index] = Reject(label, AnalyticsErrorCodes.OccurredAtOutOfRange);
                    continue;
                }

                occurredAt = clamped.Value;
            }

            var actorKey = _hasher.HashOrNull(fact.ActorUserId);
            var owner = await ResolveOwnerAsync(fact, cancellationToken).ConfigureAwait(false);

            // A member's own view of their own item is not a view. B12 already applies this rule
            // inside its own read path; applying it here as well is what keeps the two surfaces
            // agreeing, and it is also the cheapest anti-inflation measure available — the
            // commonest way a creator's figures would be wrong is the creator refreshing them.
            if (owner is not null && fact.ActorUserId == owner && IsSelfExcluded(fact.EventType))
            {
                results[index] = Reject(label, AnalyticsErrorCodes.SelfExcluded);
                continue;
            }

            var day = _calendar.DayOf(occurredAt);

            prepared[index] = new PreparedFact(
                fact,
                schema,
                occurredAt,
                day,
                actorKey,
                owner,
                DedupKeyFor(fact, schema, actorKey, day));
        }

        var keys = prepared
            .Where(entry => entry is not null)
            .Select(entry => entry!.DedupKey)
            .ToList();

        var existing = keys.Count == 0
            ? []
            : await _events.FindExistingKeysAsync(keys, cancellationToken).ConfigureAwait(false);

        var seen = new HashSet<string>(existing, StringComparer.Ordinal);
        var offset = 0;

        for (var index = 0; index < facts.Count; index++)
        {
            var entry = prepared[index];

            if (entry is null)
            {
                continue;
            }

            if (!seen.Add(entry.DedupKey))
            {
                results[index] = AnalyticsIngestionResult.Duplicate;

                AnalyticsTelemetry.EventsDuplicate.Add(
                    1, new KeyValuePair<string, object?>("event", entry.Fact.EventType));

                continue;
            }

            // Ingestion instants are staggered by one tick per row inside a batch. Not cosmetic:
            // the projection walks ingestion order, and a whole batch sharing one instant is the
            // one case where its cursor cannot step forward by taking another page. The projection
            // has a fallback for a tie group it did not create; this stops it needing one.
            _events.Add(AnalyticsEvent.Record(
                AnalyticsEventId.New(),
                entry.Fact.EventType,
                entry.Schema.Version,
                entry.Schema.TrustClass,
                entry.Fact.SourceModule,
                entry.DedupKey,
                entry.OccurredAt,
                receivedAt.AddTicks(offset++),
                entry.Day,
                entry.Fact.ContentType,
                entry.Fact.ContentId,
                entry.Owner,
                entry.ActorKey,
                entry.Fact.CountryCode,
                entry.Fact.CommunityId,
                entry.Fact.Surface,
                entry.Fact.Qualifier,
                entry.Fact.Weight,
                entry.Fact.CorrelationId,
                entry.Fact.SourceEventId));

            results[index] = AnalyticsIngestionResult.Accepted;

            AnalyticsTelemetry.EventsProcessed.Add(
                1,
                new KeyValuePair<string, object?>("event", entry.Fact.EventType),
                new KeyValuePair<string, object?>(
                    "trust", entry.Schema.TrustClass.ToString().ToLowerInvariant()));

            AnalyticsTelemetry.IngestionLag.Record(
                Math.Max(0, (receivedAt - entry.OccurredAt).TotalSeconds),
                new KeyValuePair<string, object?>("event", entry.Fact.EventType));

            if (entry.Day < _calendar.DayOf(receivedAt))
            {
                AnalyticsTelemetry.EventsLate.Add(
                    1, new KeyValuePair<string, object?>("event", entry.Fact.EventType));
            }
        }

        return results;
    }

    /// <summary>Stages one fact. The consumers' entry point.</summary>
    public async Task<AnalyticsIngestionResult> IngestAsync(
        AnalyticsFact fact, CancellationToken cancellationToken = default)
    {
        var results = await IngestAsync([fact], isClientSubmission: false, cancellationToken)
            .ConfigureAwait(false);

        return results[0];
    }

    /// <summary>
    /// Whose content this fact is about.
    /// <para>
    /// The producer's answer wins where it has one, because a published contract knows its own
    /// author and the dimension may not have caught up. Where it does not — every client submission —
    /// the dimension is asked, and a miss leaves the row unattributed rather than dropped: the
    /// publication event may still be in flight, and
    /// <see cref="AnalyticsEvent.AttributeTo"/> repairs it when the dimension arrives.
    /// </para>
    /// </summary>
    private async Task<Guid?> ResolveOwnerAsync(AnalyticsFact fact, CancellationToken cancellationToken)
    {
        if (fact.OwnerUserId is not null && fact.OwnerUserId != Guid.Empty)
        {
            return fact.OwnerUserId;
        }

        // A fact about a person rather than an item — a follow, a profile view — is attributed to
        // its subject, which is what makes it appear on that member's own overview.
        if (fact.ContentId is null || fact.ContentId == Guid.Empty)
        {
            return fact.SubjectUserId is null || fact.SubjectUserId == Guid.Empty
                ? null
                : fact.SubjectUserId;
        }

        var dimension = await _dimensions
            .FindAsync(fact.ContentType, fact.ContentId.Value, cancellationToken)
            .ConfigureAwait(false);

        return dimension?.OwnerUserId;
    }

    /// <summary>
    /// The deduplication identity for a fact.
    /// <para>
    /// <b>Derived from what the event is, never from what the caller called it.</b> A client that
    /// chose its own identifier could send a fresh one per retry and be counted twice, or a
    /// colliding one and suppress somebody else's row.
    /// </para>
    /// </summary>
    private static string DedupKeyFor(
        AnalyticsFact fact, AnalyticsEventSchema schema, string? actorKey, DateOnly day) =>
        schema.DedupStrategy switch
        {
            // "One person counted once per item per day" — the published definition of a view,
            // expressed as a key rather than as a comment on a query.
            AnalyticsDedupStrategy.PerActorPerContentPerDay => Compose(
                fact.EventType,
                ((int)fact.ContentType).ToString(CultureInfo.InvariantCulture),
                fact.ContentId?.ToString("N") ?? "~",
                actorKey ?? "~",
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),

            AnalyticsDedupStrategy.PerActorPerSubjectPerDay => Compose(
                fact.EventType,
                fact.SubjectUserId?.ToString("N") ?? fact.OwnerUserId?.ToString("N") ?? "~",
                actorKey ?? "~",
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),

            // The producing contract's own identifier, which the platform guarantees is stable
            // across retries and replays. Falls back to the composite when a producer supplies
            // none, so a consumer that forgets cannot turn every redelivery into a new row.
            _ => fact.SourceEventId is not null && fact.SourceEventId != Guid.Empty
                ? Compose(fact.EventType, fact.SourceEventId.Value.ToString("N"))
                : Compose(
                    fact.EventType,
                    ((int)fact.ContentType).ToString(CultureInfo.InvariantCulture),
                    fact.ContentId?.ToString("N") ?? "~",
                    actorKey ?? "~",
                    fact.OccurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture))
        };

    private static string Compose(params string[] parts) => string.Join('|', parts);

    /// <summary>
    /// Whether the owner's own action on their own item is excluded.
    /// <para>
    /// Views, profile views and enquiries. A creator who saves or shares their own guide has done
    /// something a reader can see, and hiding it would make the figure disagree with the item's own
    /// counters; a creator who opens their own guide has not been read by anybody. A seller who
    /// writes first in a conversation about their own listing — to somebody who made an offer, say —
    /// has not been enquired of, and the reply that follows is not an enquiry either, because it is
    /// not the conversation's first message.
    /// </para>
    /// </summary>
    private static bool IsSelfExcluded(string eventType) =>
        eventType is Domain.Policies.AnalyticsEventTypes.ContentViewed
            or Domain.Policies.AnalyticsEventTypes.ProfileViewed
            or Domain.Policies.AnalyticsEventTypes.ContentImpression
            or Domain.Policies.AnalyticsEventTypes.ContentEnquired;

    /// <summary>
    /// Brings a client's occurrence time inside the accepted window.
    /// <para>
    /// <b>Clamped forward, refused backward, and the asymmetry is deliberate.</b> A device whose
    /// clock is a few minutes fast is ordinary and dropping its telemetry would under-count exactly
    /// the members whose devices are least well looked after — so a near-future timestamp becomes
    /// now. A timestamp days in the past is a different thing: it writes into a period that has
    /// already been reported on, and accepting it would let a caller move a figure somebody has
    /// already read. The client's own buffer holds two hundred records, so the accepted window is
    /// generous enough that a genuinely offline session survives it.
    /// </para>
    /// </summary>
    private DateTimeOffset? ClampClientTime(DateTimeOffset occurredAt, DateTimeOffset now)
    {
        var skew = TimeSpan.FromMinutes(_options.MaximumClientEventSkewMinutes);
        var age = TimeSpan.FromHours(_options.MaximumClientEventAgeHours);

        if (occurredAt > now + skew)
        {
            return null;
        }

        if (occurredAt > now)
        {
            return now;
        }

        return occurredAt < now - age ? null : occurredAt;
    }

    /// <summary>
    /// Refuses one fact and counts the refusal.
    /// <para>
    /// <b><paramref name="eventLabel"/> is a registered name or <see cref="UnregisteredEvent"/>, never
    /// the caller's string</b> — see the ordering note in <see cref="IngestAsync"/>. This method took
    /// the fact itself until B24, which is how the caller's own choice of event name reached a metric
    /// dimension.
    /// </para>
    /// </summary>
    private static AnalyticsIngestionResult Reject(string eventLabel, string reason)
    {
        AnalyticsTelemetry.EventsRejected.Add(
            1,
            new KeyValuePair<string, object?>("event", eventLabel),
            new KeyValuePair<string, object?>("reason", reason));

        return AnalyticsIngestionResult.Rejected(reason);
    }

    /// <summary>
    /// The single bucket every unrecognised event name is counted under.
    /// <para>
    /// One series rather than one per name. A deployment whose clients drift produces a rising count
    /// here with <c>reason=analytics.ingest.event-unknown</c>, which is exactly the schema-drift
    /// signal the pairing exists for — and which name drifted is answered from the response body and
    /// the logs rather than from a metric that a caller can grow without limit.
    /// </para>
    /// </summary>
    private const string UnregisteredEvent = "unregistered";

    private sealed record PreparedFact(
        AnalyticsFact Fact,
        AnalyticsEventSchema Schema,
        DateTimeOffset OccurredAt,
        DateOnly Day,
        string? ActorKey,
        Guid? Owner,
        string DedupKey);
}
