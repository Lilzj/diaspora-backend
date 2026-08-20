namespace Diaspora.Platform.Messaging.Outbox.Model;

/// <summary>
/// Why a message stopped being retried.
/// <para>
/// The distinction is operational, not cosmetic. <see cref="MaxAttemptsExceeded"/> usually
/// means a consumer or a downstream dependency was down and the message is worth replaying
/// once it is back. <see cref="UnknownEventType"/> means the deployment is running code that
/// no longer knows what the message is, and replaying it will fail forever until the right
/// assembly ships. Treating both as "failed" sends an operator down the wrong path.
/// </para>
/// </summary>
public enum OutboxDeadLetterReason
{
    /// <summary>The retry budget ran out. Usually transient; usually replayable.</summary>
    MaxAttemptsExceeded = 0,

    /// <summary>
    /// The failure was classified as one that cannot succeed on a retry, so the remaining
    /// budget was skipped. Retrying a poison message only delays the dead letter.
    /// </summary>
    Poison = 1,

    /// <summary>The stored payload could not be turned back into an event.</summary>
    SerializationFailure = 2,

    /// <summary>
    /// No CLR type in this deployment answers to the stored contract name and version.
    /// Replaying will keep failing until the assembly that declares it is deployed.
    /// </summary>
    UnknownEventType = 3,

    /// <summary>An operator skipped the message deliberately.</summary>
    Skipped = 4
}
