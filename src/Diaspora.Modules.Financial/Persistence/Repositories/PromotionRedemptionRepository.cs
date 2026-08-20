using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Promotions;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for redemptions.</summary>
public sealed class PromotionRedemptionRepository : IPromotionRedemptionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public PromotionRedemptionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<PromotionRedemption> Redemptions => _dbContext.Set<PromotionRedemption>();

    public void Add(PromotionRedemption redemption)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        Redemptions.Add(redemption);
    }

    public async Task<PromotionRedemption?> FindAsync(
        PromotionRedemptionId redemptionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(redemptionId);

        return await Redemptions.FirstOrDefaultAsync(
            redemption => redemption.Id == redemptionId, cancellationToken);
    }

    public Task<int> CountForSubjectAsync(
        PromotionId promotionId, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promotionId);

        return Redemptions.CountAsync(
            redemption => redemption.PromotionId == promotionId
                && redemption.RedeemedByUserId == userId,
            cancellationToken);
    }

    public Task<int> CountForPromotionAsync(
        PromotionId promotionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promotionId);

        return Redemptions.CountAsync(
            redemption => redemption.PromotionId == promotionId, cancellationToken);
    }

    public async Task<IReadOnlyList<PromotionRedemption>> ListForUserAsync(
        Guid userId, int take, CancellationToken cancellationToken = default) =>
        await Redemptions
            .Where(redemption => redemption.RedeemedByUserId == userId)
            .OrderByDescending(redemption => redemption.RedeemedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
}
