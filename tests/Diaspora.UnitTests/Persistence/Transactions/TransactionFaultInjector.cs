namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// Decides, per attempt, where a transaction is going to fail.
/// <para>
/// Shared between the test execution strategy, the commit interceptor and the test itself, so a
/// test can say "fail the first commit acknowledgement, then succeed" and get exactly that. Every
/// failure mode B1.0.2 must survive is expressible here, which is what makes the failure-injection
/// suite deterministic rather than a race with a real database.
/// </para>
/// </summary>
public sealed class TransactionFaultInjector
{
    private int _failOperationsBeforeSave;
    private int _failOperationsAfterSave;
    private int _failCommitAcknowledgements;

    /// <summary>How many times the commit interceptor let a commit through and then threw.</summary>
    public int AcknowledgementFailures { get; private set; }

    /// <summary>
    /// Fail inside the operation before anything is written. The change tracker is untouched, so a
    /// replay is safe and the platform should retry transparently.
    /// </summary>
    public TransactionFaultInjector FailBeforeSave(int times = 1)
    {
        _failOperationsBeforeSave = times;
        return this;
    }

    /// <summary>
    /// Fail inside the operation after a save has been accepted. A replay would run against a
    /// change tracker that no longer holds the work, which is the hazard the guard exists for.
    /// </summary>
    public TransactionFaultInjector FailAfterSave(int times = 1)
    {
        _failOperationsAfterSave = times;
        return this;
    }

    /// <summary>
    /// Let the commit reach the database and succeed, then fail as though the acknowledgement was
    /// lost on the way back. The single scenario <c>verifySucceeded: null</c> could not survive.
    /// </summary>
    public TransactionFaultInjector FailCommitAcknowledgement(int times = 1)
    {
        _failCommitAcknowledgements = times;
        return this;
    }

    internal void ThrowIfFailingBeforeSave()
    {
        if (_failOperationsBeforeSave > 0)
        {
            _failOperationsBeforeSave--;
            throw new TransientTestException("Injected: the connection dropped before any write.");
        }
    }

    internal void ThrowIfFailingAfterSave()
    {
        if (_failOperationsAfterSave > 0)
        {
            _failOperationsAfterSave--;
            throw new TransientTestException("Injected: the connection dropped after a write.");
        }
    }

    internal bool ShouldFailCommitAcknowledgement()
    {
        if (_failCommitAcknowledgements <= 0)
        {
            return false;
        }

        _failCommitAcknowledgements--;
        AcknowledgementFailures++;

        return true;
    }
}
