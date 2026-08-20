using Diaspora.Modules.Guides.Domain.Drafts;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for auto-save snapshots. The prune runs through the change tracker
/// rather than a bulk delete, deliberately: the set is bounded at the retention ceiling by
/// construction, and a tracked delete keeps the write inside the same unit of work as the save
/// that triggered it.
/// </summary>
public sealed class GuideDraftSnapshotRepository : IGuideDraftSnapshotRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideDraftSnapshotRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GuideDraftSnapshot> Snapshots => _dbContext.Set<GuideDraftSnapshot>();

    public async Task<GuideDraftSnapshot?> GetAsync(
        GuideDraftSnapshotId id, CancellationToken cancellationToken = default) =>
        await Snapshots.FirstOrDefaultAsync(snapshot => snapshot.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GuideDraftSnapshot>> ListForGuideAsync(
        GuideId guideId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideId);

        return await Snapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.GuideId == guideId)
            .OrderByDescending(snapshot => snapshot.SavedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task PruneAsync(
        GuideId guideId, int keep, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideId);

        var stale = await Snapshots
            .Where(snapshot => snapshot.GuideId == guideId)
            .OrderByDescending(snapshot => snapshot.SavedAt)
            .Skip(Math.Max(0, keep))
            .ToListAsync(cancellationToken);

        if (stale.Count > 0)
        {
            Snapshots.RemoveRange(stale);
        }
    }

    public async Task AddAsync(
        GuideDraftSnapshot snapshot, CancellationToken cancellationToken = default) =>
        await Snapshots.AddAsync(snapshot, cancellationToken);
}
