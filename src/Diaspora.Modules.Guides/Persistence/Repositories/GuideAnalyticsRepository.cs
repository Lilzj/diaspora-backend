using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for analytics rows. Callers use the get-or-create pattern; the unique
/// guide index arbitrates the race between two first views, exactly as the aggregate documents.
/// </summary>
public sealed class GuideAnalyticsRepository : IGuideAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideAnalyticsRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GuideAnalytics> Analytics => _dbContext.Set<GuideAnalytics>();

    public async Task<GuideAnalytics?> GetByGuideAsync(
        GuideId guideId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideId);

        // Tracked, because every caller is about to record something on it.
        return await Analytics.FirstOrDefaultAsync(
            analytics => analytics.GuideId == guideId, cancellationToken);
    }

    public async Task AddAsync(
        GuideAnalytics analytics, CancellationToken cancellationToken = default) =>
        await Analytics.AddAsync(analytics, cancellationToken);
}
