namespace Diaspora.Persistence.Transactions;

/// <summary>
/// Whether any save inside the current platform transaction has been accepted.
/// <para>
/// The one piece of state the replay guard needs, and the only thing the save path and the
/// transactional execution path have to agree on. Once a <c>SaveChanges</c> inside the transaction
/// has accepted its changes, the tracked entities are <c>Unchanged</c> and the operation is no
/// longer replayable — a second attempt would write nothing. Set rather than inferred, because
/// "did anything get written" cannot be recovered from a change tracker after the fact.
/// </para>
/// <para>
/// <b>A type rather than a <c>bool</c> field because of who reads it.</b> It is written by the
/// save path, read by the retrying execution path, and scoped to one execution of the latter — a
/// field shared between two collaborators with no name for what it means is how the third failure
/// window gets reopened by somebody tidying up. Named, it is obvious that clearing it in the wrong
/// place turns data loss into a reported success.
/// </para>
/// </summary>
internal sealed class TransactionReplayGuard
{
    private bool _acceptedWrite;

    /// <summary>
    /// Whether a replay from here would run against a change tracker that no longer holds the
    /// work, and must therefore be refused instead.
    /// </summary>
    internal bool BlocksReplay => _acceptedWrite;

    /// <summary>
    /// Records that a save inside the transaction accepted its changes. Called from the save path,
    /// which is the only place that knows it happened.
    /// </summary>
    internal void MarkWriteAccepted() => _acceptedWrite = true;

    /// <summary>
    /// Clears the guard. Called once at the start of a whole execution and once when it ends,
    /// <b>never per attempt</b> — the guard's entire job is to notice what a <em>previous</em>
    /// attempt did.
    /// </summary>
    internal void Reset() => _acceptedWrite = false;
}
