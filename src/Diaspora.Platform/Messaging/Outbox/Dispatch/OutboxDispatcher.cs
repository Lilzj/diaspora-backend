using System.Diagnostics;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Retry;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Messaging.Outbox.Dispatch;

/// <summary>
/// Claims a batch of messages, delivers them, and records what happened to each.
/// <para>
/// <b>The one invariant every branch in this class protects: a claimed message always ends the
/// pass in a state some future pass can act on.</b> Delivered, rescheduled, dead-lettered or
/// released — never left leased by a worker that has moved on, and never dropped. Every catch
/// block here exists to uphold that, which is why the class has more error handling than
/// logic.
/// </para>
/// <para>
/// <b>Ordering is enforced by processing, not only by claiming.</b> The store refuses to claim
/// a message whose partition already has one in flight, which is most of the guarantee; the
/// rest is here, in the decision to stop a partition the moment one of its messages fails and
/// release everything behind it. Without that, a batch containing three events about one
/// provider would deliver the second and third after the first failed — in order, technically,
/// and with the "created" event missing.
/// </para>
/// <para>
/// Partitions run concurrently with each other and serially within themselves, which is the
/// whole point of partitioning: one wedged consumer costs its own aggregate's latency and
/// nobody else's.
/// </para>
/// </summary>
public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly IOutboxStore _store;
    private readonly IOutboxQueries _queries;
    private readonly IOutboxSerializer _serializer;
    private readonly IOutboxTransport _transport;
    private readonly IOutboxRetryPolicy _retryPolicy;
    private readonly IOutboxTenantResolver _tenants;
    private readonly IOutboxWorkerIdentity _worker;
    private readonly OutboxStatisticsSnapshot _snapshot;
    private readonly IClock _clock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IOutboxStore store,
        IOutboxQueries queries,
        IOutboxSerializer serializer,
        IOutboxTransport transport,
        IOutboxRetryPolicy retryPolicy,
        IOutboxTenantResolver tenants,
        IOutboxWorkerIdentity worker,
        OutboxStatisticsSnapshot snapshot,
        IClock clock,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _queries = queries;
        _serializer = serializer;
        _transport = transport;
        _retryPolicy = retryPolicy;
        _tenants = tenants;
        _worker = worker;
        _snapshot = snapshot;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OutboxDispatchResult> DispatchOnceAsync(CancellationToken cancellationToken = default)
    {
        var dispatch = _options.Dispatch;
        var now = _clock.UtcNow;

        var request = new OutboxClaimRequest(
            ClaimId: Guid.NewGuid(),
            WorkerId: _worker.WorkerId,
            BatchSize: dispatch.BatchSize,
            Now: now,
            LeaseExpiresAt: now.AddSeconds(dispatch.LeaseSeconds),
            Ordering: dispatch.Ordering,
            TenantIds: _tenants.ResolveForDispatch());

        var claimed = await _store.ClaimAsync(request, cancellationToken);

        if (claimed.Count == 0)
        {
            return OutboxDispatchResult.Empty;
        }

        using var activity = OutboxTelemetry.Activity.StartActivity(
            "outbox dispatch", ActivityKind.Consumer);

        activity?.SetTag("outbox.batch_size", claimed.Count);
        activity?.SetTag("outbox.worker", _worker.WorkerId);

        var stopwatch = Stopwatch.StartNew();

        var outcome = dispatch.Ordering == OutboxOrderingMode.None
            ? await DispatchUnorderedAsync(claimed, cancellationToken)
            : await DispatchPartitionedAsync(claimed, cancellationToken);

        stopwatch.Stop();

        OutboxTelemetry.DispatchDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        activity?.SetTag("outbox.published", outcome.Published);
        activity?.SetTag("outbox.failed", outcome.Failed);

        if (outcome.Failed > 0 || outcome.DeadLettered > 0)
        {
            _logger.LogWarning(
                "Outbox pass: {Published} published, {Failed} failed, {DeadLettered} dead-lettered, " +
                "{Deferred} deferred behind a failure, out of {Claimed} claimed.",
                outcome.Published, outcome.Failed, outcome.DeadLettered, outcome.Deferred, outcome.Claimed);
        }

        return outcome;
    }

    public async Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        var recovered = await _store.RecoverExpiredLeasesAsync(_clock.UtcNow, cancellationToken);

        if (recovered > 0)
        {
            OutboxTelemetry.LeasesRecovered.Add(recovered);

            // Warning rather than information. Every one of these is a message a worker took
            // and did not finish, which means an instance died mid-publish — invisible in every
            // other signal, because the message itself is about to be delivered successfully.
            _logger.LogWarning(
                "Recovered {Count} outbox message(s) from expired leases. A non-zero count means " +
                "a dispatcher instance stopped without releasing its claim.",
                recovered);
        }

        return recovered;
    }

    public async Task RefreshStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var statistics = await _queries.GetStatisticsAsync(_clock.UtcNow, cancellationToken);
        _snapshot.Update(statistics);
    }

    /// <summary>
    /// Every message independently. Used only under <see cref="OutboxOrderingMode.None"/>,
    /// where a failure holds nothing back.
    /// </summary>
    private async Task<OutboxDispatchResult> DispatchUnorderedAsync(
        IReadOnlyList<OutboxMessage> claimed, CancellationToken cancellationToken)
    {
        var published = 0;
        var failed = 0;
        var deadLettered = 0;

        var concurrency = Math.Max(1, _options.Dispatch.MaxConcurrency);

        foreach (var chunk in claimed.Chunk(concurrency))
        {
            var outcomes = await Task.WhenAll(
                chunk.Select(message => DeliverAsync(message, cancellationToken)));

            foreach (var outcome in outcomes)
            {
                switch (outcome)
                {
                    case DeliveryOutcome.Delivered: published++; break;
                    case DeliveryOutcome.Failed: failed++; break;
                    case DeliveryOutcome.DeadLettered: deadLettered++; break;
                }
            }
        }

        return new OutboxDispatchResult(claimed.Count, published, failed, deadLettered, Deferred: 0);
    }

    /// <summary>
    /// Partitions concurrently up to <c>Dispatch.MaxConcurrency</c>, messages within a partition
    /// in sequence order, and a stop at the first failure in each partition.
    /// <para>
    /// The bound is not a throughput tuning knob but a correctness one: everything this
    /// dispatcher touches comes from a single scope, so unbounded fan-out puts concurrent
    /// commands on one database connection. See the comment at the fan-out itself.
    /// </para>
    /// </summary>
    private async Task<OutboxDispatchResult> DispatchPartitionedAsync(
        IReadOnlyList<OutboxMessage> claimed, CancellationToken cancellationToken)
    {
        // Global ordering is partitioned ordering with exactly one partition. Expressing it
        // that way rather than as a separate code path means the two modes cannot drift, and
        // the failure semantics — stop the partition, release the rest — are identical.
        OutboxMessage[][] partitions = _options.Dispatch.Ordering == OutboxOrderingMode.Global
            ? [claimed.OrderBy(message => message.Sequence).ToArray()]
            : claimed
                .GroupBy(message => message.PartitionKey, StringComparer.Ordinal)
                .Select(group => group.OrderBy(message => message.Sequence).ToArray())
                .ToArray();

        // ── Why the fan-out is bounded ────────────────────────────────────────
        //
        // Partitions are independent of each other, but they are *not* independent of the
        // resources this dispatcher holds. `IOutboxDispatcher` is registered scoped, so
        // `_store` is one `EfOutboxStore` over one `DbContext` over one `NpgsqlConnection`,
        // and the transport publishes through handlers resolved from that same scope.
        //
        // Running every partition through `Task.WhenAll` therefore put concurrent commands on
        // a single connection, and Npgsql answered exactly as it should:
        //
        //     NpgsqlOperationInProgressException: A command is already in progress
        //
        // on the `MarkDelivered` / `MarkFailed` status write. In a live run that failed 25 of
        // 37 claimed messages per pass — the messages were published and then failed to be
        // *recorded* as published, so they retried indefinitely.
        //
        // `MaxConcurrency` already exists and the unordered path already honours it. The
        // partitioned path ignoring it was the defect. At its default of 1 the fan-out is
        // serial and correct; raising it is a deliberate statement that the store and
        // transport beneath this dispatcher can take concurrent work.
        var concurrency = Math.Max(1, _options.Dispatch.MaxConcurrency);

        OutboxDispatchResult[] results;

        if (concurrency == 1 || partitions.Length == 1)
        {
            results = new OutboxDispatchResult[partitions.Length];
            for (var index = 0; index < partitions.Length; index++)
            {
                results[index] = await DispatchPartitionAsync(partitions[index], cancellationToken);
            }
        }
        else
        {
            using var gate = new SemaphoreSlim(concurrency, concurrency);

            results = await Task.WhenAll(partitions.Select(async partition =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    return await DispatchPartitionAsync(partition, cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }));
        }

        return new OutboxDispatchResult(
            Claimed: claimed.Count,
            Published: results.Sum(result => result.Published),
            Failed: results.Sum(result => result.Failed),
            DeadLettered: results.Sum(result => result.DeadLettered),
            Deferred: results.Sum(result => result.Deferred));
    }

    private async Task<OutboxDispatchResult> DispatchPartitionAsync(
        OutboxMessage[] partition, CancellationToken cancellationToken)
    {
        var published = 0;
        var failed = 0;
        var deadLettered = 0;

        for (var index = 0; index < partition.Length; index++)
        {
            var outcome = await DeliverAsync(partition[index], cancellationToken);

            if (outcome == DeliveryOutcome.Delivered)
            {
                published++;
                continue;
            }

            switch (outcome)
            {
                case DeliveryOutcome.Failed:
                    failed++;
                    break;
                case DeliveryOutcome.DeadLettered:
                    deadLettered++;
                    break;
                // Released: the host is stopping. Not a failure, and nothing behind it should be
                // charged for one — but the partition still stops here, because delivering the
                // rest would reorder them past a message that was never attempted.
            }

            // Head-of-line stop. Everything behind this message in the partition goes back to
            // pending immediately rather than waiting out its lease, so the retry of the
            // message that failed and the messages behind it are evaluated together on a later
            // pass, in order.
            var deferred = await ReleaseRemainderAsync(partition, index + 1, cancellationToken);

            return new OutboxDispatchResult(partition.Length, published, failed, deadLettered, deferred);
        }

        return new OutboxDispatchResult(partition.Length, published, failed, deadLettered, Deferred: 0);
    }

    private async Task<int> ReleaseRemainderAsync(
        OutboxMessage[] partition, int from, CancellationToken cancellationToken)
    {
        var released = 0;
        var now = _clock.UtcNow;

        for (var index = from; index < partition.Length; index++)
        {
            var message = partition[index];

            try
            {
                // Rescheduled rather than dead-lettered, and without incrementing the attempt
                // count against it: this message has not been tried and must not spend budget
                // paying for the failure of the one in front of it.
                await _store.MarkFailedAsync(
                    message.Sequence,
                    now,
                    now,
                    $"Deferred: an earlier message in partition '{message.PartitionKey}' failed.",
                    CancellationToken.None);

                released++;
            }
#pragma warning disable CA1031 // Releasing is best-effort; the lease expiry is the backstop.
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Could not release outbox message {Sequence} after a partition failure. It " +
                    "will be recovered when its lease expires.",
                    message.Sequence);
            }
#pragma warning restore CA1031

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        return released;
    }

    /// <summary>
    /// One message, end to end: deserialize, publish, record.
    /// <para>
    /// Never throws. A dispatcher that propagated a delivery failure would abandon every
    /// message it had already claimed, leaving them leased and undeliverable until the lease
    /// lapsed — turning one bad consumer into a minute of stalled delivery for a whole batch.
    /// </para>
    /// </summary>
    private async Task<DeliveryOutcome> DeliverAsync(
        OutboxMessage message, CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var tags = new TagList
        {
            { "event.type", message.EventType },
            { "module", message.Module }
        };

        try
        {
            var metadata = _serializer.DeserializeMetadata(message.Metadata);

            var envelope = new OutboxEnvelope
            {
                EventType = message.EventType,
                EventVersion = message.EventVersion,
                Payload = message.Payload,
                EventId = message.MessageId,
                OccurredAt = message.OccurredAt,
                Module = message.Module,
                Metadata = metadata
            };

            var integrationEvent = _serializer.Deserialize(envelope);

            await PublishAsync(integrationEvent, message, metadata, cancellationToken);

            stopwatch.Stop();

            var deliveredAt = _clock.UtcNow;

            await _store.MarkDeliveredAsync(message.Sequence, deliveredAt, CancellationToken.None);

            await RecordAttemptAsync(
                message, startedAt, stopwatch.Elapsed.TotalMilliseconds, succeeded: true, error: null);

            OutboxTelemetry.Published.Add(1, tags);
            OutboxTelemetry.PublishDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            OutboxTelemetry.QueueLatency.Record(
                Math.Max(0, (deliveredAt - message.EnqueuedAt).TotalMilliseconds), tags);

            return DeliveryOutcome.Delivered;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A host shutting down is not a delivery failure. Releasing the message rather than
            // spending a retry attempt means a rolling deployment costs no retry budget at all.
            await ReleaseQuietlyAsync(message);
            return DeliveryOutcome.Released;
        }
#pragma warning disable CA1031 // Any exception a transport or consumer throws is a delivery failure.
        catch (Exception exception)
        {
            stopwatch.Stop();

            await RecordAttemptAsync(
                message,
                startedAt,
                stopwatch.Elapsed.TotalMilliseconds,
                succeeded: false,
                error: exception.ToString());

            tags.Add("exception.type", exception.GetType().Name);
            OutboxTelemetry.Failed.Add(1, tags);
            OutboxTelemetry.PublishDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

            return await ApplyRetryPolicyAsync(message, exception);
        }
#pragma warning restore CA1031
    }

    private async Task PublishAsync(
        IIntegrationEvent integrationEvent,
        OutboxMessage message,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var context = new OutboxPublishContext(
            MessageId: message.MessageId,
            Sequence: message.Sequence,
            EventType: message.EventType,
            EventVersion: message.EventVersion,
            Module: message.Module,
            PartitionKey: message.PartitionKey,
            TenantId: message.TenantId,
            CorrelationId: message.CorrelationId,
            CausationId: message.CausationId,
            OccurredAt: message.OccurredAt,
            EnqueuedAt: message.EnqueuedAt,
            AttemptNumber: message.AttemptCount + 1,
            Metadata: metadata);

        // Bounded independently of the caller's token. A transport that hangs would otherwise
        // hold its lease open until the process stopped, and every message behind it in the
        // partition with it.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.Dispatch.PublishTimeoutSeconds));

        try
        {
            await _transport.PublishAsync(integrationEvent, context, timeout.Token);
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Translated so the retry policy sees a failure rather than a cancellation, and so
            // the recorded error says what actually happened.
            throw new TimeoutException(
                $"Publishing '{message.EventType}' exceeded the " +
                $"{_options.Dispatch.PublishTimeoutSeconds}s publish timeout.");
        }
    }

    private async Task<DeliveryOutcome> ApplyRetryPolicyAsync(OutboxMessage message, Exception exception)
    {
        var now = _clock.UtcNow;

        var decision = _retryPolicy.Decide(
            new OutboxFailure(message.AttemptCount, exception, now, message.EventType));

        try
        {
            if (decision.Action == OutboxRetryAction.DeadLetter)
            {
                var reason = decision.Reason ?? OutboxDeadLetterReason.MaxAttemptsExceeded;

                await _store.DeadLetterAsync(
                    message.Sequence, reason, exception.ToString(), now, CancellationToken.None);

                OutboxTelemetry.DeadLettered.Add(
                    1,
                    new KeyValuePair<string, object?>("event.type", message.EventType),
                    new KeyValuePair<string, object?>("reason", reason.ToString()));

                // Error, not warning. A dead letter is a committed event that will not be
                // delivered without a human, which is the exact condition this phase exists to
                // make impossible to miss.
                _logger.LogError(
                    exception,
                    "Outbox message {Sequence} ({EventType}, {MessageId}) dead-lettered after " +
                    "{Attempts} attempt(s): {Reason}. The payload is retained and replayable.",
                    message.Sequence,
                    message.EventType,
                    message.MessageId,
                    message.AttemptCount + 1,
                    reason);

                return DeliveryOutcome.DeadLettered;
            }

            await _store.MarkFailedAsync(
                message.Sequence, now, decision.NextAttemptAt, exception.ToString(), CancellationToken.None);

            OutboxTelemetry.Retried.Add(
                1, new KeyValuePair<string, object?>("event.type", message.EventType));

            _logger.LogWarning(
                exception,
                "Publishing outbox message {Sequence} ({EventType}) failed on attempt " +
                "{Attempt}. Next attempt at {NextAttemptAt:O}.",
                message.Sequence,
                message.EventType,
                message.AttemptCount + 1,
                decision.NextAttemptAt);

            return DeliveryOutcome.Failed;
        }
#pragma warning disable CA1031 // The bookkeeping write failed; the lease is the backstop.
        catch (Exception bookkeeping)
        {
            // The database is unreachable, so the message cannot be marked at all. It stays
            // leased and is recovered when the lease expires — which is precisely why leases
            // exist and why they are not optional.
            _logger.LogError(
                bookkeeping,
                "Could not record the failure of outbox message {Sequence}. It remains leased " +
                "and will be recovered when the lease expires.",
                message.Sequence);

            return DeliveryOutcome.Failed;
        }
#pragma warning restore CA1031
    }

    private async Task RecordAttemptAsync(
        OutboxMessage message, DateTimeOffset startedAt, double durationMs, bool succeeded, string? error)
    {
        if (!_options.Dispatch.RecordDeliveryAttempts)
        {
            return;
        }

        try
        {
            await _store.RecordAttemptAsync(
                OutboxDeliveryAttempt.Record(
                    message.MessageId,
                    message.Sequence,
                    message.EventType,
                    message.AttemptCount + 1,
                    startedAt,
                    durationMs,
                    succeeded,
                    error,
                    _worker.WorkerId),
                CancellationToken.None);
        }
#pragma warning disable CA1031 // Diagnostics must never fail a delivery.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Could not record a delivery attempt for outbox message {Sequence}.",
                message.Sequence);
        }
#pragma warning restore CA1031
    }

    private async Task ReleaseQuietlyAsync(OutboxMessage message)
    {
        try
        {
            var now = _clock.UtcNow;

            await _store.MarkFailedAsync(
                message.Sequence,
                now,
                now,
                "Released: the host stopped before the publish completed.",
                CancellationToken.None);
        }
#pragma warning disable CA1031 // Shutdown path; the lease expiry is the backstop.
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Could not release outbox message {Sequence} during shutdown. Its lease will " +
                "expire and another instance will take it.",
                message.Sequence);
        }
#pragma warning restore CA1031
    }

    private enum DeliveryOutcome
    {
        Delivered,
        Failed,
        DeadLettered,
        Released
    }
}
