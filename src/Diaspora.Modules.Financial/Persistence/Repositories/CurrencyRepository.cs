using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for the currency catalogue.</summary>
public sealed class CurrencyRepository : ICurrencyRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CurrencyRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CurrencyDefinition> Currencies => _dbContext.Set<CurrencyDefinition>();

    public void Add(CurrencyDefinition currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        Currencies.Add(currency);
    }

    public void Update(CurrencyDefinition currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        FinancialAggregateUpdater.MarkDirty(_dbContext, currency);
    }

    public async Task<CurrencyDefinition?> FindAsync(
        CurrencyDefinitionId currencyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currencyId);

        return await Currencies.FirstOrDefaultAsync(
            currency => currency.Id == currencyId, cancellationToken);
    }

    public async Task<CurrencyDefinition?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();

        return await Currencies.FirstOrDefaultAsync(
            currency => currency.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<CurrencyDefinition>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        await Currencies
            .Where(currency => currency.IsActive)
            .OrderBy(currency => currency.SortOrder)
            .ThenBy(currency => currency.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CurrencyDefinition>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Currencies
            .OrderBy(currency => currency.SortOrder)
            .ThenBy(currency => currency.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalized = code.Trim().ToUpperInvariant();

        return Currencies.AnyAsync(currency => currency.Code == normalized, cancellationToken);
    }
}
