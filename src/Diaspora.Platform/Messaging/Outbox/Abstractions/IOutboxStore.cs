using Diaspora.Platform.Messaging.Outbox.Model;

namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// The durable operations a dispatcher performs.
/// <para>
/// Deliberately narrow. Retention, statistics and operator tooling each have their own port
/// even though one class implements all four, because the dispatcher runs several times a
/// second on every instance and its dependency surface is the one place where an accidental
/// call to a table scan does real damage. A small interface makes that mistake visible in a
/// constructor.
/// </para>
/// <para>
/// Every method here owns its own transaction. The dispatcher is not inside the producer's unit
/// of work — it is a different scope, on a different thread, minutes or days later.
/// </para>
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Leases a batch of messages for this worker and returns them in delivery order.
    /// <para>
    /// The claim is a conditional update: a message is taken only if it is still pending, its
    /// backoff has elapsed and no live lease covers it. Two workers racing for the same row
    /// produce one winner and one empty result, which is what makes horizontal scale safe
    /// without a distributed lock.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        OutboxClaimRequest request, CancellationToken cancellationToken = default);

    /// <summary>Records a successful delivery.</summary>
    Task MarkDeliveredAsync(
        long sequence, DateTimeOffset deliveredAt, CancellationToken cancellationToken = default);

    /// <summary>Records a failure and schedules the next attempt.</summary>
    Task MarkFailedAsync(
        long sequence,
        DateTimeOffset attemptedAt,
        DateTimeOffset nextAttemptAt,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a message to the dead-letter table. The message row is removed in the same
    /// transaction, so a message is never both deliverable and dead.
    /// </summary>
    Task DeadLetterAsync(
        long sequence,
        OutboxDeadLetterReason reason,
        string error,
        DateTimeOffset deadLetteredAt,
        CancellationToken cancellationToken = default);

    /// <summary>Appends an attempt record. No-op when attempt recording is off.</summary>
    Task RecordAttemptAsync(
        OutboxDeliveryAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns messages whose lease has lapsed to the pending pool.
    /// <para>
    /// The crash-recovery path, and the reason a killed worker costs latency rather than
    /// messages. Returns how many were recovered, which is a number worth alerting on: a
    /// steadily non-zero count means instances are dying mid-publish.
    /// </para>
    /// </summary>
    Task<int> RecoverExpiredLeasesAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default);
}
