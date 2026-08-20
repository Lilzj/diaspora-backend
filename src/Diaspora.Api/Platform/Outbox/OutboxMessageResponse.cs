namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// One undelivered message, as an operator sees it.
/// <para>
/// <b>The payload is not here.</b> It is the business content of a member's action — a post, a
/// verification, a provider's details — and an administration listing is the wrong place for it
/// to be read casually or copied into an incident channel. Everything needed to decide what to
/// do with the message is: what it is, how many times it has failed, why, and when it will be
/// tried again.
/// </para>
/// </summary>
public sealed record OutboxMessageResponse
{
    public required long Sequence { get; init; }

    /// <summary>The idempotency key consumers deduplicate on.</summary>
    public required Guid MessageId { get; init; }

    public required string EventType { get; init; }

    public required int EventVersion { get; init; }

    public required string Module { get; init; }

    public required string PartitionKey { get; init; }

    public string? TenantId { get; init; }

    public string? CorrelationId { get; init; }

    public required string State { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required DateTimeOffset EnqueuedAt { get; init; }

    public DateTimeOffset? NextAttemptAt { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>How long this message has been waiting. The number that decides urgency.</summary>
    public required double AgeSeconds { get; init; }

    /// <summary>The most recent failure, truncated for display.</summary>
    public string? LastError { get; init; }

    /// <summary>Which worker holds the lease, when one does.</summary>
    public string? LeaseOwner { get; init; }

    public DateTimeOffset? LeaseExpiresAt { get; init; }

    /// <summary>Payload size, so an operator can spot an oversized event without reading it.</summary>
    public required int PayloadBytes { get; init; }
}
