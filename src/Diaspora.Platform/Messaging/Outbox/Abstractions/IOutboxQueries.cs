using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Statistics;

namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// Read-only views of the outbox, for health checks, metrics and operators.
/// <para>
/// Separated from <see cref="IOutboxStore"/> because these are aggregates and scans, and the
/// dispatcher must never be one refactor away from calling one on its hot path.
/// </para>
/// </summary>
public interface IOutboxQueries
{
    /// <summary>
    /// Counts and ages across the whole queue.
    /// <para>
    /// Several aggregates over the hot table, so callers on a polling path — health probes,
    /// metric scrapes — read a cached snapshot rather than calling this directly.
    /// </para>
    /// </summary>
    Task<OutboxStatistics> GetStatisticsAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Undelivered messages, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(
        OutboxQuery query, CancellationToken cancellationToken = default);

    /// <summary>Dead letters, most recent first.</summary>
    Task<IReadOnlyList<OutboxDeadLetter>> ListDeadLettersAsync(
        OutboxQuery query, CancellationToken cancellationToken = default);

    /// <summary>One message by sequence, or null.</summary>
    Task<OutboxMessage?> FindMessageAsync(
        long sequence, CancellationToken cancellationToken = default);

    /// <summary>One dead letter by identifier, or null.</summary>
    Task<OutboxDeadLetter?> FindDeadLetterAsync(
        long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The recorded attempts for one message, oldest first. Empty when attempt recording is off.
    /// </summary>
    Task<IReadOnlyList<OutboxDeliveryAttempt>> ListAttemptsAsync(
        Guid messageId, int limit, CancellationToken cancellationToken = default);
}
