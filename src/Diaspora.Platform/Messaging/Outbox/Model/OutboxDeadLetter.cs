namespace Diaspora.Platform.Messaging.Outbox.Model;

/// <summary>
/// A message that will not be retried again, moved out of the delivery table.
/// <para>
/// <b>Moved, not flagged.</b> Leaving dead letters in <c>OutboxMessages</c> would have been
/// less code, and it would have meant every dispatcher query — the ones that run several times
/// a second on every instance — scanning past a growing population of rows that can never be
/// claimed. Two tables let the hot table stay small enough to live in cache while poison
/// messages are kept for as long as a forensic investigation needs them, which is usually far
/// longer than delivered messages are worth keeping.
/// </para>
/// <para>
/// The full payload travels with it. A dead letter that referenced a deleted message would be
/// a receipt for something nobody can replay.
/// </para>
/// </summary>
public sealed class OutboxDeadLetter
{
    private OutboxDeadLetter()
    {
        EventType = string.Empty;
        Payload = string.Empty;
        Module = string.Empty;
        PartitionKey = string.Empty;
        Error = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>
    /// The original message's idempotency key. Not unique here: a replayed dead letter that
    /// dies again produces a second row, and that history is the point.
    /// </summary>
    public Guid MessageId { get; private set; }

    /// <summary>The sequence the message held before it was moved. Diagnostic only.</summary>
    public long OriginalSequence { get; private set; }

    public string EventType { get; private set; }

    public int EventVersion { get; private set; }

    public string Payload { get; private set; }

    public string? Metadata { get; private set; }

    public string Module { get; private set; }

    public string PartitionKey { get; private set; }

    public string? TenantId { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? CausationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset EnqueuedAt { get; private set; }

    public DateTimeOffset DeadLetteredAt { get; private set; }

    public int AttemptCount { get; private set; }

    public OutboxDeadLetterReason Reason { get; private set; }

    public string Error { get; private set; }

    /// <summary>
    /// When an operator replayed this dead letter, if they did. Kept rather than deleting the
    /// row so "we already tried that on Tuesday" is answerable from the table.
    /// </summary>
    public DateTimeOffset? ReplayedAt { get; private set; }

    internal static OutboxDeadLetter From(
        OutboxMessage message,
        OutboxDeadLetterReason reason,
        string error,
        DateTimeOffset deadLetteredAt)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new OutboxDeadLetter
        {
            MessageId = message.MessageId,
            OriginalSequence = message.Sequence,
            EventType = message.EventType,
            EventVersion = message.EventVersion,
            Payload = message.Payload,
            Metadata = message.Metadata,
            Module = message.Module,
            PartitionKey = message.PartitionKey,
            TenantId = message.TenantId,
            CorrelationId = message.CorrelationId,
            CausationId = message.CausationId,
            OccurredAt = message.OccurredAt,
            EnqueuedAt = message.EnqueuedAt,
            DeadLetteredAt = deadLetteredAt,
            // The attempt that dead-lettered the message counts.
            AttemptCount = message.AttemptCount + 1,
            Reason = reason,
            Error = error
        };
    }

    public void MarkReplayed(DateTimeOffset replayedAt) => ReplayedAt = replayedAt;
}
