using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for author standing projections. The badges always travel with the row —
/// a standing without its badges would silently drop them on the next projector write, which is
/// the owned-collection trap the class summary of <c>GuideRepository</c> describes.
/// </summary>
public sealed class GuideAuthorStandingRepository : IGuideAuthorStandingRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideAuthorStandingRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GuideAuthorStanding> Standings => _dbContext.Set<GuideAuthorStanding>();

    public async Task<GuideAuthorStanding?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Standings
            .Include(standing => standing.Badges)
            .FirstOrDefaultAsync(standing => standing.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<GuideAuthorStanding>> GetManyAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return [];
        }

        // Compared as UserId, not as Guid. EF translates Contains over a value-converted
        // property; projecting .Value inside the expression makes the whole query
        // untranslatable, which is a run-time failure the compiler cannot see.
        var keys = userIds.ToList();

        return await Standings
            .AsNoTracking()
            .Include(standing => standing.Badges)
            .Where(standing => keys.Contains(standing.UserId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        GuideAuthorStanding standing, CancellationToken cancellationToken = default) =>
        await Standings.AddAsync(standing, cancellationToken);
}
