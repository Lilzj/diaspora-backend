using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for the monetization catalogue.</summary>
public sealed class MonetizationProductRepository : IMonetizationProductRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MonetizationProductRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<MonetizationProduct> Products => _dbContext.Set<MonetizationProduct>()
        .Include(product => product.Eligibility);

    public void Add(MonetizationProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);

        _dbContext.Set<MonetizationProduct>().Add(product);
    }

    public void Update(MonetizationProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);

        FinancialAggregateUpdater.MarkDirty(_dbContext, product);
    }

    public async Task<MonetizationProduct?> FindAsync(
        MonetizationProductId productId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);

        return await Products.FirstOrDefaultAsync(
            product => product.Id == productId, cancellationToken);
    }

    public async Task<MonetizationProduct?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToLowerInvariant();

        return await Products.FirstOrDefaultAsync(
            product => product.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<MonetizationProduct>> ListAvailableAsync(
        MonetizationSubjectType? subjectType,
        MonetizationProductCategory? category,
        CancellationToken cancellationToken = default)
    {
        var products = Products.Where(
            product => product.Status == MonetizationProductStatus.Active);

        if (subjectType is { } wanted)
        {
            products = products.Where(product => product.SubjectType == wanted);
        }

        if (category is { } wantedCategory)
        {
            products = products.Where(product => product.Category == wantedCategory);
        }

        return await products
            .OrderBy(product => product.SortOrder)
            .ThenBy(product => product.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonetizationProduct>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Products
            .OrderBy(product => product.SortOrder)
            .ThenBy(product => product.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalized = code.Trim().ToLowerInvariant();

        return _dbContext.Set<MonetizationProduct>().AnyAsync(
            product => product.Code == normalized, cancellationToken);
    }
}
