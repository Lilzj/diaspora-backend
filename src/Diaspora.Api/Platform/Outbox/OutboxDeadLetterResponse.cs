namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// One dead letter, as an operator sees it.
/// <para>
/// The payload is again withheld; the <see cref="Error"/> is not, because it is the entire
/// reason anybody is looking. Deciding between a replay and a skip means reading what the
/// consumer actually said.
/// </para>
/// </summary>
public sealed record OutboxDeadLetterResponse
{
    public required long Id { get; init; }

    public required Guid MessageId { get; init; }

    public required string EventType { get; init; }

    public required int EventVersion { get; init; }

    public required string Module { get; init; }

    public required string PartitionKey { get; init; }

    public string? TenantId { get; init; }

    public string? CorrelationId { get; init; }

    public required string Reason { get; init; }

    public required string Error { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required DateTimeOffset EnqueuedAt { get; init; }

    public required DateTimeOffset DeadLetteredAt { get; init; }

    /// <summary>
    /// When this dead letter was last put back on the queue, if it was. Answers "we already
    /// tried that" before somebody tries it again.
    /// </summary>
    public DateTimeOffset? ReplayedAt { get; init; }
}
