using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for the amenity catalogue.
/// <para>
/// <see cref="GetActiveKeysAsync"/> returns a set rather than a list because the amenity validator
/// asks it forty times per listing write, and a linear scan per amenity would make saving a
/// serviced apartment listing measurably slower than saving a bedsit.
/// </para>
/// </summary>
public sealed class HousingAmenityRepository : IHousingAmenityRepository
{
    private readonly DiasporaDbContext _dbContext;

    public HousingAmenityRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<HousingAmenity> Amenities => _dbContext.Set<HousingAmenity>();

    public async Task<HousingAmenity?> GetAsync(
        HousingAmenityId id, CancellationToken cancellationToken = default) =>
        await Amenities.FirstOrDefaultAsync(amenity => amenity.Id == id, cancellationToken);

    public async Task<HousingAmenity?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = HousingAmenityAssignment.NormalizeKey(key);

        return normalized is null
            ? null
            : await Amenities.AsNoTracking()
                .FirstOrDefaultAsync(amenity => amenity.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<HousingAmenity>> GetAllAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Amenities.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(amenity => amenity.IsActive);
        }

        return await query
            .OrderBy(amenity => amenity.Group)
            .ThenBy(amenity => amenity.Order)
            .ThenBy(amenity => amenity.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetActiveKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var keys = await Amenities.AsNoTracking()
            .Where(amenity => amenity.IsActive)
            .Select(amenity => amenity.Key)
            .ToListAsync(cancellationToken);

        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> KeyExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = HousingAmenityAssignment.NormalizeKey(key);

        return normalized is not null
            && await Amenities.AnyAsync(amenity => amenity.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(
        HousingAmenity amenity, CancellationToken cancellationToken = default) =>
        await Amenities.AddAsync(amenity, cancellationToken);
}
