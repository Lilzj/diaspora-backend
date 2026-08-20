using System.ComponentModel.DataAnnotations;

namespace Diaspora.Platform.Messaging.Outbox.Options;

/// <summary>
/// How the background dispatcher polls, claims and paces itself.
/// </summary>
public sealed class OutboxDispatchOptions
{
    /// <summary>
    /// Whether this instance dispatches at all.
    /// <para>
    /// Turned off, an instance still writes to the outbox — durability is never optional — and
    /// simply does not drain it. That is what lets a deployment run API pods that only enqueue
    /// and a smaller set of worker pods that only deliver, without a second binary.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long to wait before the first pass, so dispatch does not compete with migrations,
    /// seeders and the first burst of traffic.
    /// </summary>
    [Range(0, 600)]
    public int StartupDelaySeconds { get; set; } = 5;

    /// <summary>
    /// The idle poll interval. Only applies when the previous pass found nothing — a pass that
    /// filled its batch runs the next one immediately, so a backlog drains at full speed
    /// instead of one batch per tick.
    /// </summary>
    [Range(50, 300000)]
    public int PollIntervalMilliseconds { get; set; } = 1000;

    /// <summary>Messages claimed per pass.</summary>
    [Range(1, 10000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// How many messages this instance publishes at once.
    /// <para>
    /// One by default, and that is not timidity. Under <see cref="OutboxOrderingMode.Partitioned"/>
    /// concurrency is per-partition and the dispatcher already runs partitions in parallel;
    /// raising this only adds concurrency <em>within</em> a partition, which is precisely the
    /// ordering guarantee it promised not to break.
    /// </para>
    /// </summary>
    [Range(1, 256)]
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// How long a claim is held.
    /// <para>
    /// The recovery window after a worker dies mid-publish: its rows are invisible until the
    /// lease lapses, and then another instance takes them. Too short and a slow consumer's
    /// message is taken by a second worker while the first is still publishing it — allowed by
    /// at-least-once, but wasteful. Too long and a crash costs minutes of delivery latency.
    /// </para>
    /// </summary>
    [Range(5, 3600)]
    public int LeaseSeconds { get; set; } = 60;

    /// <summary>
    /// How long a single publish may take before it is abandoned and retried.
    /// <para>
    /// Must stay comfortably below <see cref="LeaseSeconds"/>. A publish that outlives its own
    /// lease is being processed by two workers at once, and the second one's success will
    /// overwrite the first one's failure bookkeeping.
    /// </para>
    /// </summary>
    [Range(1, 3600)]
    public int PublishTimeoutSeconds { get; set; } = 30;

    /// <summary>See <see cref="OutboxOrderingMode"/>.</summary>
    public OutboxOrderingMode Ordering { get; set; } = OutboxOrderingMode.Partitioned;

    /// <summary>
    /// Whether every attempt is written to its own table.
    /// <para>
    /// Off by default: the table grows at attempt rate, and a retry storm writes into it faster
    /// than anything else in the platform writes anywhere. Worth turning on while diagnosing
    /// delivery behaviour, and worth turning off again afterwards.
    /// </para>
    /// </summary>
    public bool RecordDeliveryAttempts { get; set; }

    /// <summary>
    /// How often expired leases are reclaimed. Runs on the dispatcher's own loop, so a
    /// single-instance deployment that crashes still recovers without an operator.
    /// </summary>
    [Range(5, 3600)]
    public int LeaseRecoveryIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// How long to wait for in-flight publishes when the host is stopping.
    /// <para>
    /// A graceful stop finishes what it claimed; an abrupt one leaves those rows leased and the
    /// next instance picks them up when the lease lapses. Neither loses a message — the
    /// difference is only how long the tail takes.
    /// </para>
    /// </summary>
    [Range(0, 120)]
    public int ShutdownDrainSeconds { get; set; } = 10;
}
