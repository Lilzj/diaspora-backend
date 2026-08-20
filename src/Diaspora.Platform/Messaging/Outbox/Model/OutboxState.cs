namespace Diaspora.Platform.Messaging.Outbox.Model;

/// <summary>
/// Where a message is in its delivery lifecycle.
/// <para>
/// Persisted as a string rather than an ordinal. An outbox row can outlive several
/// deployments, and a renumbered enum silently reinterprets every row already in the table —
/// which is the one failure mode a durability component cannot afford.
/// </para>
/// </summary>
public enum OutboxState
{
    /// <summary>
    /// Written and committed, not yet delivered. Eligible for claiming once
    /// <c>NextAttemptAt</c> has passed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Leased by a dispatcher instance. Invisible to other workers until the lease expires,
    /// which is what makes a crashed worker's backlog recoverable rather than stuck.
    /// </summary>
    Claimed = 1,

    /// <summary>Published successfully. Retained until the retention window closes.</summary>
    Delivered = 2
}
