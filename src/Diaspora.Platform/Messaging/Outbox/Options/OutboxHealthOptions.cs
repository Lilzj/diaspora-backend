using System.ComponentModel.DataAnnotations;

namespace Diaspora.Platform.Messaging.Outbox.Options;

/// <summary>
/// The thresholds that turn outbox statistics into a health verdict.
/// <para>
/// Depth alone is a poor signal: a queue of ten thousand that is draining is healthy, and a
/// queue of one that has not moved in an hour is not. The age of the oldest pending message is
/// the one number that distinguishes them, which is why it carries the tighter thresholds.
/// </para>
/// </summary>
public sealed class OutboxHealthOptions
{
    /// <summary>Pending depth above which readiness degrades.</summary>
    [Range(1, 10000000)]
    public int DegradedDepth { get; set; } = 5000;

    /// <summary>Pending depth above which readiness fails.</summary>
    [Range(1, 10000000)]
    public int UnhealthyDepth { get; set; } = 50000;

    /// <summary>Age of the oldest undelivered message at which readiness degrades.</summary>
    [Range(1, 86400)]
    public int DegradedOldestPendingSeconds { get; set; } = 120;

    /// <summary>
    /// Age of the oldest undelivered message at which readiness fails. Ten minutes of no
    /// progress is a stuck dispatcher, not a busy one.
    /// </summary>
    [Range(1, 86400)]
    public int UnhealthyOldestPendingSeconds { get; set; } = 600;

    /// <summary>
    /// Dead letters above which readiness degrades.
    /// <para>
    /// Never unhealthy on this signal alone. A dead letter means delivery already stopped for
    /// that message; failing readiness would take an instance out of rotation for something
    /// taking it out of rotation cannot fix.
    /// </para>
    /// </summary>
    [Range(0, 10000000)]
    public int DegradedDeadLetters { get; set; } = 1;

    /// <summary>
    /// How long a statistics snapshot is reused.
    /// <para>
    /// Health endpoints get polled every few seconds by every probe in the cluster, and the
    /// statistics query is several aggregates over the hot table. Caching briefly is the
    /// difference between a health check and a load generator.
    /// </para>
    /// </summary>
    [Range(1, 300)]
    public int SnapshotCacheSeconds { get; set; } = 5;
}
