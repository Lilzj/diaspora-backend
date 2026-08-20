namespace Diaspora.Platform.Messaging.Outbox.Statistics;

/// <summary>
/// A point-in-time picture of the queue, as read by health checks, metrics gauges and the
/// administration API.
/// <para>
/// <b><see cref="OldestPendingAt"/> is the one that matters.</b> Depth alone cannot distinguish
/// a queue of ten thousand draining at full speed from a queue of one that has not moved since
/// lunchtime, and only the second is an incident. Every threshold in the health check leans on
/// the age of the oldest message for that reason.
/// </para>
/// </summary>
public sealed record OutboxStatistics
{
    public static readonly OutboxStatistics Empty = new();

    /// <summary>Messages written and not yet delivered, including those waiting on backoff.</summary>
    public int Pending { get; init; }

    /// <summary>Messages currently leased by some worker.</summary>
    public int Claimed { get; init; }

    /// <summary>
    /// Pending messages that have failed at least once. The retry backlog: distinct from depth
    /// because a large depth of never-attempted messages is throughput, and a large retry
    /// backlog is something being refused.
    /// </summary>
    public int Retrying { get; init; }

    /// <summary>Delivered messages still inside the retention window.</summary>
    public int Delivered { get; init; }

    /// <summary>Dead letters currently retained.</summary>
    public int DeadLettered { get; init; }

    /// <summary>When the oldest undelivered message was enqueued, or null when the queue is empty.</summary>
    public DateTimeOffset? OldestPendingAt { get; init; }

    /// <summary>When this snapshot was taken.</summary>
    public DateTimeOffset ObservedAt { get; init; }

    public IReadOnlyList<OutboxModuleStatistics> ByModule { get; init; } = [];

    public IReadOnlyList<OutboxEventTypeStatistics> ByEventType { get; init; } = [];

    /// <summary>
    /// How long the oldest undelivered message has been waiting. Zero when the queue is empty,
    /// which reads correctly on a dashboard where "no data" does not.
    /// </summary>
    public TimeSpan OldestPendingAge =>
        OldestPendingAt is { } oldest && ObservedAt > oldest
            ? ObservedAt - oldest
            : TimeSpan.Zero;
}
