using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for financial identities.</summary>
public sealed class FinancialAccountRepository : IFinancialAccountRepository
{
    private readonly DiasporaDbContext _dbContext;

    public FinancialAccountRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<FinancialAccount> Accounts => _dbContext.Set<FinancialAccount>();

    public void Add(FinancialAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        Accounts.Add(account);
    }

    public void Update(FinancialAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        FinancialAggregateUpdater.MarkDirty(_dbContext, account);
    }

    public async Task<FinancialAccount?> FindAsync(
        FinancialAccountId accountId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        return await Accounts.FirstOrDefaultAsync(
            account => account.Id == accountId, cancellationToken);
    }

    public async Task<FinancialAccount?> FindByOwnerAsync(
        Guid ownerId, FinancialAccountType accountType, CancellationToken cancellationToken = default) =>
        await Accounts.FirstOrDefaultAsync(
            account => account.OwnerId == ownerId && account.AccountType == accountType,
            cancellationToken);

    public async Task<FinancialAccount?> FindPlatformAccountAsync(
        CancellationToken cancellationToken = default) =>
        await Accounts
            .OrderBy(account => account.CreatedAt)
            .FirstOrDefaultAsync(account => account.IsPlatformAccount, cancellationToken);

    public Task<bool> ExistsForOwnerAsync(
        Guid ownerId, FinancialAccountType accountType, CancellationToken cancellationToken = default) =>
        Accounts.AnyAsync(
            account => account.OwnerId == ownerId && account.AccountType == accountType,
            cancellationToken);
}
