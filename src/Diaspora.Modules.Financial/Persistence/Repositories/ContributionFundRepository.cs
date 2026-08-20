using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for community funds.</summary>
public sealed class ContributionFundRepository : IContributionFundRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ContributionFundRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ContributionFund> Funds => _dbContext.Set<ContributionFund>();

    public void Add(ContributionFund fund)
    {
        ArgumentNullException.ThrowIfNull(fund);

        Funds.Add(fund);
    }

    public void Update(ContributionFund fund)
    {
        ArgumentNullException.ThrowIfNull(fund);

        FinancialAggregateUpdater.MarkDirty(_dbContext, fund);
    }

    public async Task<ContributionFund?> FindAsync(
        ContributionFundId fundId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundId);

        return await Funds.FirstOrDefaultAsync(fund => fund.Id == fundId, cancellationToken);
    }

    public async Task<ContributionFund?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToLowerInvariant();

        return await Funds.FirstOrDefaultAsync(fund => fund.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<ContributionFund>> ListActiveAsync(
        string? countryCode, CancellationToken cancellationToken = default)
    {
        var funds = Funds.Where(fund => fund.IsActive);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();

            funds = funds.Where(fund => fund.CountryCode == null || fund.CountryCode == country);
        }

        return await funds
            .OrderBy(fund => fund.SortOrder)
            .ThenBy(fund => fund.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContributionFund>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Funds.OrderBy(fund => fund.SortOrder).ThenBy(fund => fund.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalized = code.Trim().ToLowerInvariant();

        return Funds.AnyAsync(fund => fund.Code == normalized, cancellationToken);
    }
}
