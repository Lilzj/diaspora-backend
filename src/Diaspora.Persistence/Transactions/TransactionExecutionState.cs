using Diaspora.Platform.Transactions;

namespace Diaspora.Persistence.Transactions;

/// <summary>
/// What one transactional execution carries across the execution strategy's attempts.
/// <para>
/// The execution strategy hands this back to both the operation and the verification delegate, so
/// it is the only place the two can agree on anything. Three of its fields are load-bearing:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="ReceiptId"/> is generated once and reused by every attempt, so verification asks
/// "did <em>this</em> transaction commit" rather than "did any of them".
/// </item>
/// <item>
/// <see cref="Result"/> is captured before the commit is attempted. If the commit turns out to
/// have landed after all, the strategy returns this rather than replaying — the caller gets the
/// result of work that really did happen.
/// </item>
/// <item>
/// <see cref="CommitAttempted"/> distinguishes a failure during the operation, where the receipt
/// was never written and verification would be a wasted round trip, from a failure at the commit,
/// where it is the only thing that can answer the question.
/// </item>
/// </list>
/// <para>
/// A class rather than a struct: the strategy passes it by value to each delegate, and the state
/// each attempt records has to be visible to the next one.
/// </para>
/// </summary>
internal sealed class TransactionExecutionState<TResult>
{
    public TransactionExecutionState(Func<CancellationToken, Task<TResult>> operation)
    {
        Operation = operation;
        ReceiptId = TransactionReceipt.NewId();
    }

    public Func<CancellationToken, Task<TResult>> Operation { get; }

    /// <summary>Stable for the whole execution, across every retry of it.</summary>
    public string ReceiptId { get; }

    /// <summary>How many times the strategy has entered the operation.</summary>
    public int Attempts { get; set; }

    /// <summary>Set once the commit has been reached, cleared when a fresh attempt begins.</summary>
    public bool CommitAttempted { get; set; }

    /// <summary>The operation's result, captured before the commit was attempted.</summary>
    public TResult? Result { get; set; }

    /// <summary>
    /// The failure that ended the last attempt. Carried so that when the platform refuses to
    /// replay, the exception it raises names the real cause rather than only its own verdict.
    /// </summary>
    public Exception? LastFailure { get; set; }
}
