namespace Diaspora.Platform.Messaging.Outbox.Statistics;

/// <summary>
/// One module's slice of the queue.
/// <para>
/// The breakdown that turns "the outbox is backed up" into "Community is backed up". Without
/// it, an operator seeing a depth of forty thousand has to query the table by hand during an
/// incident to learn which module produced them.
/// </para>
/// </summary>
/// <param name="Module">The producing module.</param>
/// <param name="Pending">Undelivered messages.</param>
/// <param name="DeadLettered">Dead letters currently retained.</param>
/// <param name="OldestPendingAt">When the oldest undelivered message was enqueued.</param>
public sealed record OutboxModuleStatistics(
    string Module,
    int Pending,
    int DeadLettered,
    DateTimeOffset? OldestPendingAt);
