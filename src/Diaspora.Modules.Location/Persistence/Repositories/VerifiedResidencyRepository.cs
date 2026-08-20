using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence.Repositories;

/// <summary>EF Core repository for the projection of Trust's residency decisions.</summary>
public sealed class VerifiedResidencyRepository : IVerifiedResidencyRepository
{
    private readonly DiasporaDbContext _dbContext;

    public VerifiedResidencyRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<VerifiedResidency> Residencies => _dbContext.Set<VerifiedResidency>();

    public async Task<VerifiedResidency?> GetAsync(
        UserId userId, CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(countryCode);

        // Tracked: the only caller is the event handler that is about to apply a decision.
        return await Residencies.FirstOrDefaultAsync(
            residency => residency.UserId == userId && residency.CountryCode == countryCode,
            cancellationToken);
    }

    public async Task<IReadOnlyList<VerifiedResidency>> ListByUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Residencies
            .AsNoTracking()
            .Where(residency => residency.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<UserId, IReadOnlyList<VerifiedResidency>>> ListByUsersAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<UserId, IReadOnlyList<VerifiedResidency>>();
        }

        var wanted = userIds.Distinct().ToList();

        var residencies = await Residencies
            .AsNoTracking()
            .Where(residency => wanted.Contains(residency.UserId))
            .ToListAsync(cancellationToken);

        return residencies
            .GroupBy(residency => residency.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<VerifiedResidency>)group.ToList());
    }

    public async Task AddAsync(
        VerifiedResidency residency, CancellationToken cancellationToken = default) =>
        await Residencies.AddAsync(residency, cancellationToken);
}
