using Diaspora.Platform.Messaging.Outbox.Model;

namespace Diaspora.Platform.Messaging.Outbox.Retry;

/// <summary>
/// The retry policy's verdict on one failure.
/// </summary>
/// <param name="Action">Retry or stop.</param>
/// <param name="NextAttemptAt">
/// When the next attempt becomes eligible. Equal to now for an immediate retry. Ignored when
/// the action is <see cref="OutboxRetryAction.DeadLetter"/>.
/// </param>
/// <param name="Reason">Why the message is being dead-lettered. Null when retrying.</param>
public sealed record OutboxRetryDecision(
    OutboxRetryAction Action,
    DateTimeOffset NextAttemptAt,
    OutboxDeadLetterReason? Reason)
{
    public static OutboxRetryDecision Retry(DateTimeOffset nextAttemptAt) =>
        new(OutboxRetryAction.Retry, nextAttemptAt, null);

    public static OutboxRetryDecision DeadLetter(DateTimeOffset now, OutboxDeadLetterReason reason) =>
        new(OutboxRetryAction.DeadLetter, now, reason);
}
