namespace Diaspora.Platform.Messaging.Outbox.Model;

/// <summary>
/// One integration event, durably recorded, awaiting delivery.
/// <para>
/// <b>Deliberately not an aggregate root.</b> It implements neither <c>IHasDomainEvents</c>
/// nor <c>ISoftDeletable</c> nor <c>IAuditableEntity</c>, and that is load-bearing in all three
/// cases: an outbox row that raised domain events would enqueue outbox rows for its own
/// creation forever, a soft-deleted outbox row would be invisible to the dispatcher's query
/// filter while still occupying the table, and audit stamping would rewrite <c>ModifiedAt</c>
/// on every lease renewal for no reader's benefit.
/// </para>
/// <para>
/// <b>The primary key is <see cref="Sequence"/>, not <see cref="MessageId"/>.</b> A database
/// identity column is the only ordering source that survives clock skew between application
/// instances, and ordering is a delivery guarantee this platform makes. <see cref="MessageId"/>
/// carries the originating event's identifier instead, uniquely indexed, because that is the
/// value a consumer deduplicates on — the two identities answer different questions and
/// collapsing them would cost one of the answers.
/// </para>
/// <para>
/// State transitions live on this class rather than in the store so that every path through
/// them is visible in one file and testable without a database.
/// </para>
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Required by EF Core's materializer.</summary>
    private OutboxMessage()
    {
        EventType = string.Empty;
        Payload = string.Empty;
        Module = string.Empty;
        PartitionKey = string.Empty;
    }

    private OutboxMessage(
        Guid messageId,
        string eventType,
        int eventVersion,
        string payload,
        string? metadata,
        string module,
        string partitionKey,
        string? tenantId,
        string? correlationId,
        string? causationId,
        DateTimeOffset occurredAt,
        DateTimeOffset enqueuedAt)
    {
        MessageId = messageId;
        EventType = eventType;
        EventVersion = eventVersion;
        Payload = payload;
        Metadata = metadata;
        Module = module;
        PartitionKey = partitionKey;
        TenantId = tenantId;
        CorrelationId = correlationId;
        CausationId = causationId;
        OccurredAt = occurredAt;
        EnqueuedAt = enqueuedAt;
        State = OutboxState.Pending;
        NextAttemptAt = enqueuedAt;
    }

    /// <summary>
    /// Database-assigned insertion order. The dispatcher's only ordering source.
    /// </summary>
    public long Sequence { get; private set; }

    /// <summary>
    /// The originating integration event's <c>EventId</c>. Unique, and the value consumers
    /// are told to deduplicate on.
    /// </summary>
    public Guid MessageId { get; private set; }

    /// <summary>The stable contract name of the event type. Never an assembly-qualified name.</summary>
    public string EventType { get; private set; }

    /// <summary>The contract version the payload was written against.</summary>
    public int EventVersion { get; private set; }

    /// <summary>The serialized event body.</summary>
    public string Payload { get; private set; }

    /// <summary>Serialized metadata bag, or null when the event carried none.</summary>
    public string? Metadata { get; private set; }

    /// <summary>The module that produced the event, for per-module statistics and triage.</summary>
    public string Module { get; private set; }

    /// <summary>
    /// The ordering partition. Messages sharing a key are delivered in <see cref="Sequence"/>
    /// order and never concurrently; messages in different partitions have no ordering
    /// relationship at all.
    /// </summary>
    public string PartitionKey { get; private set; }

    /// <summary>
    /// The owning tenant, or null in a single-tenant deployment. Present from day one because
    /// adding a discriminator to a table already holding millions of rows is a migration
    /// nobody schedules willingly.
    /// </summary>
    public string? TenantId { get; private set; }

    /// <summary>The correlation identifier of the request that produced the event.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>The identifier of the message or request that caused this one.</summary>
    public string? CausationId { get; private set; }

    /// <summary>When the domain fact happened, as reported by the event itself.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When the row was written. Differs from <see cref="OccurredAt"/> under replay.</summary>
    public DateTimeOffset EnqueuedAt { get; private set; }

    public OutboxState State { get; private set; }

    /// <summary>How many delivery attempts have been made.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// The earliest moment a dispatcher may claim this message. Set forward by the retry
    /// policy to implement backoff, which is why backoff needs no in-memory timer and
    /// survives a restart.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>The most recent failure, truncated. Kept for triage, not for logic.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Identifies one claim operation, so a worker can read back exactly the rows its own
    /// conditional update won — without a transaction held open across the read.
    /// </summary>
    public Guid? ClaimId { get; private set; }

    /// <summary>Which worker holds the lease. Diagnostic; the lease itself is the guard.</summary>
    public string? LeaseOwner { get; private set; }

    /// <summary>
    /// When the lease lapses. A worker that dies mid-publish leaves its rows claimed; this is
    /// what lets another instance take them back rather than the backlog stalling forever.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public static OutboxMessage Create(
        Guid messageId,
        string eventType,
        int eventVersion,
        string payload,
        string? metadata,
        string module,
        string partitionKey,
        string? tenantId,
        string? correlationId,
        string? causationId,
        DateTimeOffset occurredAt,
        DateTimeOffset enqueuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        return new OutboxMessage(
            messageId,
            eventType,
            eventVersion,
            payload,
            metadata,
            module,
            partitionKey,
            tenantId,
            correlationId,
            causationId,
            occurredAt,
            enqueuedAt);
    }

    /// <summary>
    /// Rebuilds a message from a dead letter being replayed.
    /// <para>
    /// The original <see cref="MessageId"/> is preserved on purpose. A replay is the same fact
    /// being delivered again, and a consumer that already processed it must be able to
    /// recognise that from the idempotency key alone.
    /// </para>
    /// </summary>
    public static OutboxMessage Replay(OutboxDeadLetter deadLetter, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(deadLetter);

        return new OutboxMessage(
            deadLetter.MessageId,
            deadLetter.EventType,
            deadLetter.EventVersion,
            deadLetter.Payload,
            deadLetter.Metadata,
            deadLetter.Module,
            deadLetter.PartitionKey,
            deadLetter.TenantId,
            deadLetter.CorrelationId,
            deadLetter.CausationId,
            deadLetter.OccurredAt,
            now);
    }

    /// <summary>Records a successful publish.</summary>
    public void MarkDelivered(DateTimeOffset deliveredAt)
    {
        State = OutboxState.Delivered;
        DeliveredAt = deliveredAt;
        LastAttemptAt = deliveredAt;
        AttemptCount++;
        NextAttemptAt = null;
        ClaimId = null;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        LastError = null;
    }

    /// <summary>Records a failed publish and schedules the next attempt.</summary>
    public void MarkFailed(DateTimeOffset attemptedAt, DateTimeOffset nextAttemptAt, string error)
    {
        State = OutboxState.Pending;
        AttemptCount++;
        LastAttemptAt = attemptedAt;
        NextAttemptAt = nextAttemptAt;
        LastError = Truncate(error);
        ClaimId = null;
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    /// <summary>
    /// Returns the message to the pending pool immediately, ignoring backoff. Used by an
    /// operator forcing a retry and by lease recovery.
    /// </summary>
    public void Release(DateTimeOffset now)
    {
        State = OutboxState.Pending;
        NextAttemptAt = now;
        ClaimId = null;
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    /// <summary>Turns the message into the dead letter that replaces it.</summary>
    public OutboxDeadLetter ToDeadLetter(
        OutboxDeadLetterReason reason, string error, DateTimeOffset deadLetteredAt) =>
        OutboxDeadLetter.From(this, reason, Truncate(error), deadLetteredAt);

    /// <summary>
    /// Errors are stored for a human reading a triage screen, and a stack trace from a deeply
    /// nested handler can run to tens of kilobytes. Truncating at the boundary keeps one bad
    /// deployment from turning the outbox table into a log sink.
    /// </summary>
    internal static string Truncate(string value) =>
        value.Length <= OutboxFieldLimits.Error ? value : value[..OutboxFieldLimits.Error];
}
