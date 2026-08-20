namespace Diaspora.Platform.Operations.Policies;

/// <summary>What a consumer does when its queue is growing faster than it drains.</summary>
public enum BackpressureMode
{
    /// <summary>Keep accepting. The queue grows until something else stops it. Honest, and rarely right.</summary>
    None = 0,

    /// <summary>Slow the producer by refusing enqueues above the high-water mark.</summary>
    RejectEnqueue = 1,

    /// <summary>Park the producer until the queue drains below the low-water mark.</summary>
    BlockProducer = 2,

    /// <summary>
    /// Drop the newest work. Only defensible for telemetry and other data whose value expires —
    /// never for anything a member is waiting on.
    /// </summary>
    ShedNewest = 3
}

/// <summary>
/// How a queue is consumed and when it is considered to be in trouble.
/// <para>
/// The outbox has carried numbers of this shape since B1.1; B17 gives every other queue — the
/// notification dispatcher, the recommendation refresh, the media pipeline — the same vocabulary
/// so one alert rule can be written against all of them.
/// </para>
/// <para>
/// <b>Depth alone is not a signal.</b> A queue of ten thousand that drains in four seconds is
/// healthy; a queue of forty that has not moved in an hour is an incident. That is why
/// <see cref="MaxOldestAge"/> exists beside <see cref="DepthWarningThreshold"/>, and why the
/// dashboards show both.
/// </para>
/// </summary>
public sealed record QueuePolicy : OperationalPolicy
{
    public const string PolicyKind = "queue";

    public QueuePolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>Consumers run in parallel against this queue on one instance.</summary>
    public int Concurrency { get; init; } = 1;

    /// <summary>
    /// Upper bound <see cref="AdaptiveThroughput"/> may raise <see cref="Concurrency"/> to. A
    /// ceiling exists because the cost of another consumer is not paid by the queue — it is paid by
    /// the database connection pool underneath it.
    /// </summary>
    public int MaxConcurrency { get; init; } = 8;

    /// <summary>Messages claimed per poll.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// Whether the consumer may raise and lower its own concurrency between
    /// <see cref="Concurrency"/> and <see cref="MaxConcurrency"/> as depth changes.
    /// </summary>
    public bool AdaptiveThroughput { get; init; }

    /// <summary>What happens when depth passes <see cref="DepthCriticalThreshold"/>.</summary>
    public BackpressureMode Backpressure { get; init; } = BackpressureMode.None;

    /// <summary>Depth at which the dashboard warns and the alert policy may fire.</summary>
    public int DepthWarningThreshold { get; init; } = 1_000;

    /// <summary>Depth at which backpressure engages.</summary>
    public int DepthCriticalThreshold { get; init; } = 10_000;

    /// <summary>
    /// Age of the oldest undelivered item beyond which the queue is unhealthy regardless of depth.
    /// This is the signal that catches a stalled consumer, which depth never does.
    /// </summary>
    public TimeSpan MaxOldestAge { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Consumer group this queue's consumers join. Distinct groups each receive every message;
    /// consumers within a group share them. The transport in use today is in-process and ignores
    /// this — it is declared so a broker adapter does not require a policy change.
    /// </summary>
    public string ConsumerGroup { get; init; } = "default";

    public override IEnumerable<string> Validate()
    {
        if (Concurrency < 1)
        {
            yield return "Concurrency must be at least 1.";
        }

        if (MaxConcurrency < Concurrency)
        {
            yield return "MaxConcurrency is below Concurrency.";
        }

        if (BatchSize < 1)
        {
            yield return "BatchSize must be at least 1.";
        }

        if (DepthWarningThreshold < 0 || DepthCriticalThreshold < 0)
        {
            yield return "Depth thresholds cannot be negative.";
        }

        if (DepthCriticalThreshold < DepthWarningThreshold)
        {
            yield return "DepthCriticalThreshold is below DepthWarningThreshold, so the warning could never fire first.";
        }

        if (MaxOldestAge <= TimeSpan.Zero)
        {
            yield return "MaxOldestAge must be positive.";
        }

        if (string.IsNullOrWhiteSpace(ConsumerGroup))
        {
            yield return "ConsumerGroup cannot be blank.";
        }

        if (AdaptiveThroughput && MaxConcurrency == Concurrency)
        {
            yield return "AdaptiveThroughput is enabled but MaxConcurrency equals Concurrency, leaving nothing to adapt.";
        }
    }
}
