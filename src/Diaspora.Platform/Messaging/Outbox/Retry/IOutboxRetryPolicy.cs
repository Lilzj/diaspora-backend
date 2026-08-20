namespace Diaspora.Platform.Messaging.Outbox.Retry;

/// <summary>
/// Decides whether a failed message is tried again, and when.
/// <para>
/// The whole of "never spin forever" lives behind this one method. Every path through it either
/// schedules a bounded, capped delay or gives up — there is no branch that returns "try again
/// straight away, indefinitely", and a custom implementation that adds one is the only way this
/// component can loop.
/// </para>
/// </summary>
public interface IOutboxRetryPolicy
{
    OutboxRetryDecision Decide(OutboxFailure failure);
}
