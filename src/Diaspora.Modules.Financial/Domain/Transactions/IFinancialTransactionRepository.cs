using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Transactions;

/// <summary>Retrieval and staging for transactions.</summary>
public interface IFinancialTransactionRepository
{
    void Add(FinancialTransaction transaction);

    void Update(FinancialTransaction transaction);

    Task<FinancialTransaction?> FindAsync(
        TransactionId transactionId, CancellationToken cancellationToken = default);

    /// <summary>The idempotency check. A duplicate reference is a replayed request.</summary>
    Task<FinancialTransaction?> FindByReferenceAsync(
        string reference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialTransaction>> PageAsync(
        TransactionQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialTransaction>> ListForPeriodAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
