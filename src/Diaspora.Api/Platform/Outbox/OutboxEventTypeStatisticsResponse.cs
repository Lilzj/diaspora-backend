namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// One event contract's share of the queue.
/// <para>
/// <see cref="MaxAttempts"/> is the early-warning field: a contract whose pending messages are
/// on their sixth attempt is on its way to the dead-letter queue and has not arrived yet.
/// </para>
/// </summary>
public sealed record OutboxEventTypeStatisticsResponse
{
    public required string EventType { get; init; }

    public required int Pending { get; init; }

    public required int DeadLettered { get; init; }

    public required int MaxAttempts { get; init; }
}
