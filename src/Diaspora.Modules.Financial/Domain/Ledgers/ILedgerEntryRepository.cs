using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Ledgers;

/// <summary>
/// Retrieval and staging for ledger entries.
/// <para>
/// <b>There is no <c>Update</c> and no <c>Remove</c>, on purpose.</b> The repository's shape is part
/// of how the ledger's immutability is enforced: a caller cannot stage a modification because there
/// is no method that takes one.
/// </para>
/// </summary>
public interface ILedgerEntryRepository
{
    void Add(LedgerEntry entry);

    void AddRange(IEnumerable<LedgerEntry> entries);

    Task<LedgerEntry?> FindAsync(
        LedgerEntryId entryId, CancellationToken cancellationToken = default);

    /// <summary>A page of a wallet's statement, newest first.</summary>
    Task<IReadOnlyList<LedgerEntry>> PageAsync(
        LedgerQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every entry for a wallet, oldest first. The rebuild path.</summary>
    Task<IReadOnlyList<LedgerEntry>> ListForRebuildAsync(
        WalletId walletId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerEntry>> ListByTransactionAsync(
        TransactionId transactionId, CancellationToken cancellationToken = default);

    Task<long> CountForWalletAsync(
        WalletId walletId, CancellationToken cancellationToken = default);

    /// <summary>Entries in a window, for the revenue rollup.</summary>
    Task<IReadOnlyList<LedgerEntry>> ListForPeriodAsync(
        WalletId walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
