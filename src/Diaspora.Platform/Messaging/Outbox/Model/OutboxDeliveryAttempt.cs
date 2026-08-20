namespace Diaspora.Platform.Messaging.Outbox.Model;

/// <summary>
/// One recorded publish attempt.
/// <para>
/// Optional by configuration, and off by default on a high-volume deployment, because the
/// table grows at attempt rate rather than message rate — a retry storm writes more rows here
/// than the outbox itself holds. It earns its keep during an incident, when the question is
/// "was this failing all afternoon or only after the deploy", which no counter can answer and
/// no log retention window reliably covers.
/// </para>
/// </summary>
public sealed class OutboxDeliveryAttempt
{
    private OutboxDeliveryAttempt()
    {
        EventType = string.Empty;
        WorkerId = string.Empty;
    }

    public long Id { get; private set; }

    public Guid MessageId { get; private set; }

    public long MessageSequence { get; private set; }

    public string EventType { get; private set; }

    public int AttemptNumber { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public double DurationMilliseconds { get; private set; }

    public bool Succeeded { get; private set; }

    public string? Error { get; private set; }

    public string WorkerId { get; private set; }

    public static OutboxDeliveryAttempt Record(
        Guid messageId,
        long messageSequence,
        string eventType,
        int attemptNumber,
        DateTimeOffset startedAt,
        double durationMilliseconds,
        bool succeeded,
        string? error,
        string workerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        return new OutboxDeliveryAttempt
        {
            MessageId = messageId,
            MessageSequence = messageSequence,
            EventType = eventType,
            AttemptNumber = attemptNumber,
            StartedAt = startedAt,
            DurationMilliseconds = durationMilliseconds,
            Succeeded = succeeded,
            Error = error is null ? null : OutboxMessage.Truncate(error),
            WorkerId = workerId
        };
    }
}
