using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for reward accounts.
/// <para>
/// Every read includes the entries. They are the aggregate — a partially loaded account that a
/// handler then saves would have EF write an empty entry collection over a real one, which here
/// would be a member's whole earning history.
/// </para>
/// </summary>
public sealed class RewardAccountRepository : IRewardAccountRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RewardAccountRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<RewardAccount> Accounts =>
        _dbContext.Set<RewardAccount>().Include(account => account.Entries);

    public void Add(RewardAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        _dbContext.Set<RewardAccount>().Add(account);
    }

    public void Update(RewardAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        FinancialAggregateUpdater.MarkDirty(_dbContext, account);
    }

    public async Task<RewardAccount?> FindAsync(
        RewardAccountId accountId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        return await Accounts.FirstOrDefaultAsync(
            account => account.Id == accountId, cancellationToken);
    }

    public async Task<RewardAccount?> FindByMemberAsync(
        Guid memberUserId, CancellationToken cancellationToken = default)
    {
        // Staged first: a grant opens the account and awards against it in one unit of work, and a
        // lookup that only asked the database would open a second one for the index to refuse.
        var staged = _dbContext.Set<RewardAccount>().Local.FirstOrDefault(
            account => account.MemberUserId == memberUserId);

        return staged ?? await Accounts.FirstOrDefaultAsync(
            account => account.MemberUserId == memberUserId, cancellationToken);
    }
}
