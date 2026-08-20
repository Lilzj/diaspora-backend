using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for the accommodation-type catalogue.
/// <para>
/// The catalogue is small by construction — a few dozen rows — so it is read whole rather than
/// paged. Every key is normalised on the way in, so a caller passing "Shared Room" and one passing
/// "shared-room" find the same type rather than one of them finding nothing.
/// </para>
/// </summary>
public sealed class AccommodationTypeRepository : IAccommodationTypeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AccommodationTypeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AccommodationType> Types => _dbContext.Set<AccommodationType>();

    public async Task<AccommodationType?> GetAsync(
        AccommodationTypeId id, CancellationToken cancellationToken = default) =>
        await Types.FirstOrDefaultAsync(type => type.Id == id, cancellationToken);

    public async Task<AccommodationType?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = HousingListing.NormalizeTypeKey(key);

        return normalized is null
            ? null
            : await Types.AsNoTracking()
                .FirstOrDefaultAsync(type => type.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<AccommodationType>> GetAllAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Types.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(type => type.IsActive);
        }

        return await query
            .OrderBy(type => type.Order)
            .ThenBy(type => type.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = HousingListing.NormalizeTypeKey(key);

        return normalized is not null
            && await Types.AnyAsync(type => type.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(
        AccommodationType type, CancellationToken cancellationToken = default) =>
        await Types.AddAsync(type, cancellationToken);
}
