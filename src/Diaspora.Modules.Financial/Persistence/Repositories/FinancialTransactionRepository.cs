using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for transactions.
/// <para>
/// <b>Paged by ADR-017's rule: exclude by identity and accumulate while the boundary holds.</b> A
/// timestamp-only cursor silently drops rows tied on the timestamp, and the keyset predicate cannot
/// be written when the tie-break column is a strongly typed identifier the provider cannot order.
/// </para>
/// </summary>
public sealed class FinancialTransactionRepository : IFinancialTransactionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public FinancialTransactionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<FinancialTransaction> Transactions => _dbContext.Set<FinancialTransaction>();

    public void Add(FinancialTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        Transactions.Add(transaction);
    }

    public void Update(FinancialTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        FinancialAggregateUpdater.MarkDirty(_dbContext, transaction);
    }

    public async Task<FinancialTransaction?> FindAsync(
        TransactionId transactionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        return await Transactions.FirstOrDefaultAsync(
            transaction => transaction.Id == transactionId, cancellationToken);
    }

    public async Task<FinancialTransaction?> FindByReferenceAsync(
        string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var normalized = reference.Trim();

        return await Transactions.FirstOrDefaultAsync(
            transaction => transaction.Reference == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> PageAsync(
        TransactionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var transactions = Transactions.AsQueryable();

        if (query.UserId is { } userId)
        {
            transactions = transactions.Where(
                transaction => transaction.InitiatedByUserId == userId);
        }

        if (query.TransactionType is { } transactionType)
        {
            transactions = transactions.Where(
                transaction => transaction.TransactionType == transactionType);
        }

        if (query.Status is { } status)
        {
            transactions = transactions.Where(transaction => transaction.Status == status);
        }

        if (query.From is { } from)
        {
            transactions = transactions.Where(transaction => transaction.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            transactions = transactions.Where(transaction => transaction.OccurredAt < to);
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            var country = query.CountryCode.Trim().ToUpperInvariant();

            transactions = transactions.Where(transaction => transaction.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            var currency = query.Currency.Trim().ToUpperInvariant();

            transactions = transactions.Where(transaction => transaction.Currency == currency);
        }

        if (query.Before is { } before)
        {
            transactions = transactions.Where(transaction => transaction.OccurredAt <= before);
        }

        // Exclude by identity rather than by a strict timestamp comparison, so rows sharing the
        // boundary instant are served once and never dropped.
        if (query.ExcludeIds.Count > 0)
        {
            var excluded = query.ExcludeIds
                .Select(TransactionId.From)
                .ToList();

            transactions = transactions.Where(
                transaction => !excluded.Contains(transaction.Id));
        }

        return await transactions
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Take(Math.Clamp(query.PageSize, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> ListForPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        await Transactions
            .Where(transaction => transaction.OccurredAt >= from && transaction.OccurredAt < to)
            .OrderBy(transaction => transaction.OccurredAt)
            .ToListAsync(cancellationToken);
}
