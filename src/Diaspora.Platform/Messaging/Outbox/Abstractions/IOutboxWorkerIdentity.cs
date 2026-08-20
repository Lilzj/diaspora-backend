namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// Who this dispatcher instance is.
/// <para>
/// Stamped onto every lease and every recorded attempt. In a single-instance deployment it is
/// noise; the moment there are three pods and one of them is holding a lease that never
/// expires, it is the only thing in the database that says which pod to look at.
/// </para>
/// </summary>
public interface IOutboxWorkerIdentity
{
    /// <summary>
    /// A stable identifier for this process. Must differ between instances — two workers sharing
    /// an identity can each release the other's lease.
    /// </summary>
    string WorkerId { get; }
}
