namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// What a transport needs to know about a message beyond the event itself.
/// <para>
/// Passed as a parameter rather than pushed into an ambient context because the dispatcher runs
/// on a background thread that has no request behind it. Anything a transport reads from
/// ambient state on that thread is either empty or belongs to somebody else's message.
/// </para>
/// </summary>
/// <param name="MessageId">The idempotency key, equal to the event's own identifier.</param>
/// <param name="Sequence">Write order. Meaningful within a partition.</param>
/// <param name="EventType">The stable contract name.</param>
/// <param name="EventVersion">The contract version the payload was written against.</param>
/// <param name="Module">The module that produced the event.</param>
/// <param name="PartitionKey">The ordering partition.</param>
/// <param name="TenantId">The owning tenant, or null in a single-tenant deployment.</param>
/// <param name="CorrelationId">The originating request's correlation identifier.</param>
/// <param name="CausationId">What caused this message.</param>
/// <param name="OccurredAt">When the fact happened.</param>
/// <param name="EnqueuedAt">When the row was written.</param>
/// <param name="AttemptNumber">
/// Which attempt this is, starting at 1. A transport that is idempotent on its own — most
/// brokers, given a deduplication window — can use this to skip work it already did.
/// </param>
/// <param name="Metadata">Everything else the envelope carried.</param>
public sealed record OutboxPublishContext(
    Guid MessageId,
    long Sequence,
    string EventType,
    int EventVersion,
    string Module,
    string PartitionKey,
    string? TenantId,
    string? CorrelationId,
    string? CausationId,
    DateTimeOffset OccurredAt,
    DateTimeOffset EnqueuedAt,
    int AttemptNumber,
    IReadOnlyDictionary<string, string> Metadata);
