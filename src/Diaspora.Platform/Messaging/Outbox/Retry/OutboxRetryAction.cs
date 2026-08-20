namespace Diaspora.Platform.Messaging.Outbox.Retry;

/// <summary>What the dispatcher does with a message that failed to publish.</summary>
public enum OutboxRetryAction
{
    /// <summary>Schedule another attempt at the decided time.</summary>
    Retry = 0,

    /// <summary>Stop. The message moves to the dead-letter table with a reason.</summary>
    DeadLetter = 1
}
