using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for the ledger.
/// <para>
/// <b>There is no <c>Update</c> and no <c>Remove</c> here, and there never will be.</b> The
/// repository's shape is part of how the ledger's immutability is enforced: a caller cannot stage a
/// modification because no method takes one.
/// </para>
/// <para>
/// <b>Paged by sequence rather than by timestamp.</b> Two entries in one transaction share a
/// timestamp to the microsecond; a timestamp-only cursor silently drops one of them, which the
/// platform has on record as the worst property a paging bug can have. The per-wallet sequence is
/// gapless, so the keyset predicate is exact and needs no tie-break list.
/// </para>
/// </summary>
public sealed class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public LedgerEntryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<LedgerEntry> Entries => _dbContext.Set<LedgerEntry>();

    public void Add(LedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entries.Add(entry);
    }

    public void AddRange(IEnumerable<LedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries.AddRange(entries);
    }

    public async Task<LedgerEntry?> FindAsync(
        LedgerEntryId entryId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryId);

        return await Entries.FirstOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerEntry>> PageAsync(
        LedgerQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entries = Entries.Where(entry => entry.WalletId == query.WalletId);

        if (query.AfterSequence is { } sequence)
        {
            entries = entries.Where(entry => entry.Sequence < sequence);
        }

        if (query.From is { } from)
        {
            entries = entries.Where(entry => entry.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            entries = entries.Where(entry => entry.OccurredAt < to);
        }

        if (query.EntryType is { } entryType)
        {
            entries = entries.Where(entry => entry.EntryType == entryType);
        }

        if (query.Direction is { } direction)
        {
            entries = entries.Where(entry => entry.Direction == direction);
        }

        if (query.Bucket is { } bucket)
        {
            entries = entries.Where(entry => entry.Bucket == bucket);
        }

        return await entries
            .OrderByDescending(entry => entry.Sequence)
            .Take(Math.Clamp(query.PageSize, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerEntry>> ListForRebuildAsync(
        WalletId walletId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(walletId);

        return await Entries
            .Where(entry => entry.WalletId == walletId)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerEntry>> ListByTransactionAsync(
        TransactionId transactionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        return await Entries
            .Where(entry => entry.TransactionId == transactionId)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountForWalletAsync(
        WalletId walletId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(walletId);

        return await Entries.LongCountAsync(
            entry => entry.WalletId == walletId, cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerEntry>> ListForPeriodAsync(
        WalletId walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(walletId);

        return await Entries
            .Where(entry => entry.WalletId == walletId
                && entry.OccurredAt >= from
                && entry.OccurredAt < to)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken);
    }
}
