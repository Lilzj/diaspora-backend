namespace Diaspora.Platform.Messaging.Outbox.Statistics;

/// <summary>
/// One event contract's slice of the queue.
/// <para>
/// Finer than the per-module view and answering a different question: which specific contract
/// is failing. A module with one broken consumer and nine healthy ones looks unhealthy at
/// module granularity and obvious at this one.
/// </para>
/// </summary>
/// <param name="EventType">The stable contract name.</param>
/// <param name="Pending">Undelivered messages of this type.</param>
/// <param name="DeadLettered">Dead letters of this type currently retained.</param>
/// <param name="MaxAttempts">
/// The highest attempt count among the pending messages. A rising number here is a consumer
/// failing repeatedly without having exhausted its budget yet — the earliest visible sign of a
/// dead letter that has not happened.
/// </param>
public sealed record OutboxEventTypeStatistics(
    string EventType,
    int Pending,
    int DeadLettered,
    int MaxAttempts);
