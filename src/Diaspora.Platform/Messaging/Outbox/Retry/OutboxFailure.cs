namespace Diaspora.Platform.Messaging.Outbox.Retry;

/// <summary>
/// Everything the retry policy is allowed to look at.
/// <para>
/// A record rather than a pile of parameters so the policy is trivially unit-testable without
/// a database, a clock or a message — which matters, because retry behaviour is the part of a
/// delivery pipeline nobody can observe in production until it is already wrong.
/// </para>
/// </summary>
/// <param name="AttemptCount">
/// Attempts already made, before this one. Zero on a first failure. Read from the row, so it
/// survives a restart and a crash loop cannot reset the budget.
/// </param>
/// <param name="Exception">What the transport threw.</param>
/// <param name="Now">The dispatcher's clock reading, never <c>DateTimeOffset.UtcNow</c>.</param>
/// <param name="EventType">The contract name, for policies that vary by event.</param>
public sealed record OutboxFailure(
    int AttemptCount,
    Exception Exception,
    DateTimeOffset Now,
    string EventType);
