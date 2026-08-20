using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Promotions;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for promotions.</summary>
public sealed class PromotionRepository : IPromotionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public PromotionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Promotion> Promotions => _dbContext.Set<Promotion>();

    public void Add(Promotion promotion)
    {
        ArgumentNullException.ThrowIfNull(promotion);

        Promotions.Add(promotion);
    }

    public void Update(Promotion promotion)
    {
        ArgumentNullException.ThrowIfNull(promotion);

        FinancialAggregateUpdater.MarkDirty(_dbContext, promotion);
    }

    public async Task<Promotion?> FindAsync(
        PromotionId promotionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promotionId);

        return await Promotions.FirstOrDefaultAsync(
            promotion => promotion.Id == promotionId, cancellationToken);
    }

    public async Task<Promotion?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();

        return await Promotions.FirstOrDefaultAsync(
            promotion => promotion.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Promotion>> ListActiveAsync(
        DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        await Promotions
            .Where(promotion => promotion.Status == PromotionStatus.Active
                && promotion.Window.StartsAt <= asOf
                && (promotion.Window.EndsAt == null || promotion.Window.EndsAt > asOf))
            .OrderBy(promotion => promotion.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Promotion>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Promotions.OrderBy(promotion => promotion.Code).ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalized = code.Trim().ToUpperInvariant();

        return Promotions.AnyAsync(promotion => promotion.Code == normalized, cancellationToken);
    }
}
