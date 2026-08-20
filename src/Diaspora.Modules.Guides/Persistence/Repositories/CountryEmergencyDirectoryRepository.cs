using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for emergency information.
/// <para>
/// There is no <c>Remove</c> and no read past a query filter, because this is the one aggregate in
/// the phase that is never soft-deleted. Both absences are load-bearing: a repository that offered a
/// removal would make it possible to hide a country's emergency numbers behind a flag, which is
/// exactly what the aggregate's design refuses.
/// </para>
/// </summary>
public sealed class CountryEmergencyDirectoryRepository : ICountryEmergencyDirectoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CountryEmergencyDirectoryRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<CountryEmergencyDirectory> Directories =>
        _dbContext.Set<CountryEmergencyDirectory>();

    public async Task<CountryEmergencyDirectory?> GetAsync(
        CountryEmergencyDirectoryId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Directories)
            .FirstOrDefaultAsync(directory => directory.Id == id, cancellationToken);

    public async Task<CountryEmergencyDirectory?> GetForCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        return country is null
            ? null
            : await FullGraph(Directories)
                .FirstOrDefaultAsync(
                    directory => directory.CountryCode == country, cancellationToken);
    }

    public async Task<bool> ExistsForCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        return country is not null
            && await Directories.AnyAsync(
                directory => directory.CountryCode == country, cancellationToken);
    }

    public async Task<IReadOnlyList<CountryEmergencyDirectory>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await FullGraph(Directories.AsNoTracking())
            .OrderBy(directory => directory.CountryCode)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        CountryEmergencyDirectory directory, CancellationToken cancellationToken = default) =>
        await Directories.AddAsync(directory, cancellationToken);

    private static IQueryable<CountryEmergencyDirectory> FullGraph(
        IQueryable<CountryEmergencyDirectory> query) =>
        query
            .Include(directory => directory.Contacts)
            .Include(directory => directory.Localizations);
}
