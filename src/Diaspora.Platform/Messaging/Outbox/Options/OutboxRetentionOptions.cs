using System.ComponentModel.DataAnnotations;

namespace Diaspora.Platform.Messaging.Outbox.Options;

/// <summary>
/// What is kept, and for how long.
/// <para>
/// An outbox with no retention is a table that grows for the lifetime of the product, and it
/// grows fastest on exactly the deployments that can least afford it. The delivered rows are
/// the bulk of that; they are kept at all only so that "did this event go out on Tuesday" has
/// an answer for a few days.
/// </para>
/// </summary>
public sealed class OutboxRetentionOptions
{
    /// <summary>Whether the retention sweep runs at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long a delivered message is kept. Three days covers a weekend incident review and
    /// is short enough that the hot table stays small.
    /// </summary>
    [Range(1, 8760)]
    public int DeliveredRetentionHours { get; set; } = 72;

    /// <summary>
    /// How long a dead letter is kept.
    /// <para>
    /// Far longer than a delivered message, because a dead letter is unfinished business. Thirty
    /// days is enough for the message to survive the deployment that broke it, the investigation
    /// and the fix — and a dead letter purged before it is replayed is an event permanently
    /// lost, which is the failure this whole phase exists to prevent.
    /// </para>
    /// </summary>
    [Range(1, 8760)]
    public int DeadLetterRetentionHours { get; set; } = 720;

    /// <summary>How long recorded delivery attempts are kept, when recording is on.</summary>
    [Range(1, 8760)]
    public int AttemptRetentionHours { get; set; } = 168;

    /// <summary>How often the sweep runs.</summary>
    [Range(1, 1440)]
    public int SweepIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Rows deleted per statement.
    /// <para>
    /// Bounded so a first sweep against a table that has been accumulating for a month takes a
    /// series of short locks rather than one long one. The sweep repeats until the batch comes
    /// back short.
    /// </para>
    /// </summary>
    [Range(1, 100000)]
    public int PurgeBatchSize { get; set; } = 1000;

    /// <summary>
    /// Ceiling on batches per sweep, so a very large backlog cannot hold a connection for the
    /// whole interval. What is left is collected by the next pass.
    /// </summary>
    [Range(1, 10000)]
    public int MaxBatchesPerSweep { get; set; } = 50;
}
