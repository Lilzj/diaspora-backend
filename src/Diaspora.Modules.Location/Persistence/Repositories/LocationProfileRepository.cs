using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence.Repositories;

/// <summary>
/// EF Core repository for the member's geographic context.
/// <para>
/// Always tracked, and always with both owned collections. Every caller either mutates the
/// profile or renders the whole selector — there is no read path that wants the profile
/// without its saved and recent locations, so loading them lazily would only ever cost two
/// extra round trips.
/// </para>
/// </summary>
public sealed class LocationProfileRepository : ILocationProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public LocationProfileRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<LocationProfile> Profiles => _dbContext.Set<LocationProfile>();

    public async Task<LocationProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Profiles
            .Include(profile => profile.SavedLocations)
            .Include(profile => profile.RecentLocations)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(
        LocationProfile profile, CancellationToken cancellationToken = default) =>
        await Profiles.AddAsync(profile, cancellationToken);
}
