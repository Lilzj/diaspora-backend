namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// The operations that change the queue from outside the delivery loop: retention sweeps and
/// operator repairs.
/// <para>
/// Kept behind its own port because everything here either deletes rows or resurrects them. A
/// component that only needs to publish should not be able to purge, and a constructor
/// parameter is the cheapest place in the codebase to enforce that.
/// </para>
/// </summary>
public interface IOutboxMaintenance
{
    /// <summary>
    /// Deletes delivered messages enqueued before the cutoff, up to <paramref name="batchSize"/>
    /// rows. Returns how many were removed, so the caller can loop until a batch comes back
    /// short rather than guessing at the backlog.
    /// </summary>
    Task<int> PurgeDeliveredAsync(
        DateTimeOffset deliveredBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Deletes dead letters older than the cutoff.</summary>
    Task<int> PurgeDeadLettersAsync(
        DateTimeOffset deadLetteredBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Deletes recorded attempts older than the cutoff.</summary>
    Task<int> PurgeAttemptsAsync(
        DateTimeOffset startedBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues a dead letter under its original message identifier and stamps the dead
    /// letter as replayed. Returns false when the identifier matches nothing.
    /// </summary>
    Task<bool> ReplayDeadLetterAsync(
        long deadLetterId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues every dead letter matching the filter. Returns how many were replayed.
    /// </summary>
    Task<int> ReplayDeadLettersAsync(
        OutboxQuery query, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a pending message's remaining backoff so the next pass claims it. Returns false
    /// when the sequence matches nothing claimable.
    /// </summary>
    Task<bool> RetryNowAsync(
        long sequence, DateTimeOffset now, CancellationToken cancellationToken = default);
}
