using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Statistics;

namespace Diaspora.Platform.Messaging.Outbox.Administration;

/// <summary>
/// The operator's controls: inspect the queue, replay what failed, skip what cannot succeed,
/// and collect what is finished.
/// <para>
/// <b>An outbox without these is a black box with a growing table.</b> Every one of these
/// operations is something a team will need at three in the morning, and the alternative to
/// providing them is a psql session against a production database written from memory under
/// pressure — which is how a replay becomes a deletion.
/// </para>
/// <para>
/// Replay preserves the original message identifier. Consumers are told to deduplicate on it,
/// so a replay is safe by construction for any consumer that followed the guidance, and
/// visible in metadata for any that did not.
/// </para>
/// </summary>
public interface IOutboxAdministration
{
    /// <summary>Current depth, ages and per-module breakdown.</summary>
    Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>Undelivered messages, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(
        OutboxQuery query, CancellationToken cancellationToken = default);

    /// <summary>Dead letters, most recent first.</summary>
    Task<IReadOnlyList<OutboxDeadLetter>> ListDeadLettersAsync(
        OutboxQuery query, CancellationToken cancellationToken = default);

    /// <summary>The recorded attempts for one message.</summary>
    Task<IReadOnlyList<OutboxDeliveryAttempt>> ListAttemptsAsync(
        Guid messageId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts one dead letter back on the queue with a fresh sequence and a reset retry budget.
    /// </summary>
    Task<OutboxOperationResult> ReplayDeadLetterAsync(
        long deadLetterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays every dead letter matching the filter, up to the query's limit.
    /// <para>
    /// The limit is not advisory. Replaying thirty thousand dead letters at once recreates the
    /// load that produced them, against a dependency that has only just recovered.
    /// </para>
    /// </summary>
    Task<OutboxOperationResult> ReplayDeadLettersAsync(
        OutboxQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a pending message immediately, ignoring its remaining backoff. For the case
    /// where the dependency is known to be back and nobody wants to wait out a ten-minute delay.
    /// </summary>
    Task<OutboxOperationResult> RetryNowAsync(
        long sequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives up on a pending message and dead-letters it with the operator's reason.
    /// <para>
    /// The escape hatch for a genuine poison message that is blocking its partition. It never
    /// deletes: the payload survives in the dead-letter table, so a decision made in a hurry
    /// can be reversed by a replay.
    /// </para>
    /// </summary>
    Task<OutboxOperationResult> SkipAsync(
        long sequence, string reason, CancellationToken cancellationToken = default);

    /// <summary>Removes delivered messages older than the configured retention window.</summary>
    Task<OutboxOperationResult> PurgeDeliveredAsync(CancellationToken cancellationToken = default);
}
