using System.ComponentModel.DataAnnotations;

namespace Diaspora.Platform.Messaging.Outbox.Options;

/// <summary>
/// Everything the outbox can be told, in one bound section.
/// <para>
/// <b>Owned by the platform, configured by the deployment, invisible to modules.</b> No
/// business module reads this type or any of its parts. That is the boundary B1.1 set out to
/// hold: a module publishes an integration event and says nothing about how it is delivered,
/// so changing batch sizes, retry curves or transports never touches module code.
/// </para>
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Platform:Outbox";

    /// <summary>
    /// The master switch.
    /// <para>
    /// Off, integration events publish in-process exactly as they did before B1.1 — the escape
    /// hatch for a deployment that hits an unforeseen problem in the outbox and needs its old
    /// behaviour back without a rollback. It is the pre-B1.1 behaviour in full, including the
    /// data loss, so it is not somewhere to stay.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum accepted payload size. A payload larger than this is refused at enqueue time,
    /// inside the producer's transaction, where the failure is attributable — rather than at
    /// dispatch time, where it would be a dead letter nobody can trace to a call site.
    /// </summary>
    [Range(1024, 33554432)]
    public int MaxPayloadBytes { get; set; } = 1_048_576;

    public OutboxDispatchOptions Dispatch { get; set; } = new();

    public OutboxRetryOptions Retry { get; set; } = new();

    public OutboxRetentionOptions Retention { get; set; } = new();

    public OutboxHealthOptions Health { get; set; } = new();
}
