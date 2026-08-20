using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// Lets a commit reach the database and succeed, then throws — the lost acknowledgement.
/// <para>
/// <b>This is the scenario the whole patch is about, and it is the only one that cannot be
/// simulated by throwing somewhere convenient.</b> Every other injected failure leaves the
/// database unchanged; this one leaves it changed while telling the application it failed. An
/// execution strategy that retries here without verifying writes everything twice.
/// </para>
/// <para>
/// EF invokes <c>TransactionCommitted</c> after the underlying commit has returned, so throwing
/// from it reproduces the real shape exactly: committed on the server, failed on the client.
/// </para>
/// </summary>
public sealed class CommitAcknowledgementFaultInterceptor : DbTransactionInterceptor
{
    private readonly TransactionFaultInjector _injector;

    public CommitAcknowledgementFaultInterceptor(TransactionFaultInjector injector)
    {
        _injector = injector;
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        Throw();
        base.TransactionCommitted(transaction, eventData);
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Throw();
        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    private void Throw()
    {
        if (_injector.ShouldFailCommitAcknowledgement())
        {
            throw new TransientTestException(
                "Injected: the commit succeeded but the acknowledgement was lost.");
        }
    }
}
