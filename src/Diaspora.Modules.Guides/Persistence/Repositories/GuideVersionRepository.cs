using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for version snapshots. Append-only in practice as well as in spirit:
/// there is no removal path, because history outlives the guide it records.
/// </summary>
public sealed class GuideVersionRepository : IGuideVersionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideVersionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GuideVersion> Versions => _dbContext.Set<GuideVersion>();

    public async Task<GuideVersion?> GetAsync(
        GuideVersionId id, CancellationToken cancellationToken = default) =>
        await Versions.FirstOrDefaultAsync(version => version.Id == id, cancellationToken);

    public async Task<GuideVersion?> GetByNumberAsync(
        GuideId guideId, int versionNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideId);

        return await Versions.FirstOrDefaultAsync(
            version => version.GuideId == guideId && version.VersionNumber == versionNumber,
            cancellationToken);
    }

    public async Task<IReadOnlyList<GuideVersion>> ListForGuideAsync(
        GuideId guideId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideId);

        return await Versions
            .AsNoTracking()
            .Where(version => version.GuideId == guideId)
            .OrderByDescending(version => version.VersionNumber)
            .Take(Math.Clamp(limit, 1, GuideLimits.MaxVersionsListed))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        GuideVersion version, CancellationToken cancellationToken = default) =>
        await Versions.AddAsync(version, cancellationToken);
}
