using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for credit grants.</summary>
public sealed class CreditGrantRepository : ICreditGrantRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreditGrantRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreditGrant> Grants => _dbContext.Set<CreditGrant>();

    public void Add(CreditGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        Grants.Add(grant);
    }

    public void Update(CreditGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        FinancialAggregateUpdater.MarkDirty(_dbContext, grant);
    }

    public async Task<CreditGrant?> FindAsync(
        CreditGrantId grantId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grantId);

        return await Grants.FirstOrDefaultAsync(grant => grant.Id == grantId, cancellationToken);
    }

    public async Task<IReadOnlyList<CreditGrant>> ListForWalletAsync(
        WalletId walletId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(walletId);

        return await Grants
            .Where(grant => grant.WalletId == walletId)
            .OrderByDescending(grant => grant.GrantedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreditGrant>> ListForOwnerAsync(
        Guid ownerId, CancellationToken cancellationToken = default) =>
        await Grants
            .Where(grant => grant.OwnerId == ownerId)
            .OrderByDescending(grant => grant.GrantedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CreditGrant>> ListExpiredAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Grants
            .Where(grant => !grant.IsRevoked
                && !grant.IsExpired
                && grant.ExpiresAt != null
                && grant.ExpiresAt <= asOf)
            .OrderBy(grant => grant.ExpiresAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
}
