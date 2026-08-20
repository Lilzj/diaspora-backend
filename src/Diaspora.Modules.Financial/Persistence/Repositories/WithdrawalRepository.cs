using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for payout requests.</summary>
public sealed class WithdrawalRepository : IWithdrawalRepository
{
    private readonly DiasporaDbContext _dbContext;

    public WithdrawalRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Withdrawal> Withdrawals => _dbContext.Set<Withdrawal>();

    public void Add(Withdrawal withdrawal)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        Withdrawals.Add(withdrawal);
    }

    public void Update(Withdrawal withdrawal)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        FinancialAggregateUpdater.MarkDirty(_dbContext, withdrawal);
    }

    public async Task<Withdrawal?> FindAsync(
        WithdrawalId withdrawalId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(withdrawalId);

        return await Withdrawals.FirstOrDefaultAsync(
            withdrawal => withdrawal.Id == withdrawalId, cancellationToken);
    }

    public async Task<Withdrawal?> FindByIdempotencyKeyAsync(
        Guid requestedByUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || requestedByUserId == Guid.Empty)
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        // Staged first: a retried request inside one unit of work has not been saved yet, and the
        // unique index would refuse the second insert with an error naming a column and nothing else.
        //
        // Scoped to the requester on both paths. Matching on the key alone answered one
        // member's withdrawal request with another member's withdrawal — its amount, its
        // destination label, its status — and created nothing for the caller.
        var staged = Withdrawals.Local.FirstOrDefault(withdrawal =>
            string.Equals(withdrawal.IdempotencyKey, normalized, StringComparison.Ordinal)
            && withdrawal.RequestedByUserId == requestedByUserId);

        return staged ?? await Withdrawals.FirstOrDefaultAsync(
            withdrawal => withdrawal.IdempotencyKey == normalized
                && withdrawal.RequestedByUserId == requestedByUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Withdrawal>> ListForUserAsync(
        Guid userId,
        WithdrawalStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        var withdrawals = Withdrawals.Where(withdrawal => withdrawal.RequestedByUserId == userId);

        if (status is { } wanted)
        {
            withdrawals = withdrawals.Where(withdrawal => withdrawal.Status == wanted);
        }

        if (before is { } boundary)
        {
            withdrawals = withdrawals.Where(withdrawal => withdrawal.RequestedAt <= boundary);
        }

        if (excludeIds is { Count: > 0 })
        {
            var excluded = excludeIds.Select(WithdrawalId.From).ToList();

            withdrawals = withdrawals.Where(withdrawal => !excluded.Contains(withdrawal.Id));
        }

        return await withdrawals
            .OrderByDescending(withdrawal => withdrawal.RequestedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// What a member has committed today.
    /// <para>
    /// Counts held, in-flight and completed requests and excludes only the refused ones. A daily cap
    /// that counted completed payouts alone would let somebody raise a hundred requests before the
    /// first settled and defeat itself entirely.
    /// </para>
    /// </summary>
    public async Task<long> SumSinceAsync(
        Guid userId,
        string currency,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        return await Withdrawals
            .Where(withdrawal => withdrawal.RequestedByUserId == userId
                && withdrawal.Currency == normalized
                && withdrawal.RequestedAt >= since
                && withdrawal.Status != WithdrawalStatus.Failed
                && withdrawal.Status != WithdrawalStatus.Cancelled)
            .SumAsync(withdrawal => withdrawal.GrossMinorUnits, cancellationToken);
    }

    public async Task<IReadOnlyList<Withdrawal>> ListPendingAsync(
        int take, CancellationToken cancellationToken = default) =>
        await Withdrawals
            .Where(withdrawal => withdrawal.Status == WithdrawalStatus.Requested)
            .OrderBy(withdrawal => withdrawal.RequestedAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
}
