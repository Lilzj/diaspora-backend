using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for donation causes.</summary>
public sealed class DonationCauseRepository : IDonationCauseRepository
{
    private readonly DiasporaDbContext _dbContext;

    public DonationCauseRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<DonationCause> Causes => _dbContext.Set<DonationCause>();

    public void Add(DonationCause cause)
    {
        ArgumentNullException.ThrowIfNull(cause);

        Causes.Add(cause);
    }

    public void Update(DonationCause cause)
    {
        ArgumentNullException.ThrowIfNull(cause);

        FinancialAggregateUpdater.MarkDirty(_dbContext, cause);
    }

    public async Task<DonationCause?> FindAsync(
        DonationCauseId causeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(causeId);

        return await Causes.FirstOrDefaultAsync(cause => cause.Id == causeId, cancellationToken);
    }

    public async Task<DonationCause?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToLowerInvariant();

        return await Causes.FirstOrDefaultAsync(cause => cause.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<DonationCause>> ListActiveAsync(
        string? countryCode, CancellationToken cancellationToken = default)
    {
        var causes = Causes.Where(cause => cause.IsActive);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();

            causes = causes.Where(cause => cause.CountryCode == null || cause.CountryCode == country);
        }

        return await causes
            .OrderBy(cause => cause.Urgency)
            .ThenByDescending(cause => cause.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DonationCause>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Causes.OrderBy(cause => cause.Code).ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalized = code.Trim().ToLowerInvariant();

        return Causes.AnyAsync(cause => cause.Code == normalized, cancellationToken);
    }
}
