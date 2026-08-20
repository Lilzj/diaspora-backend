using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Analytics;

/// <summary>
/// Reads and writes analytics rows. Callers use the get-or-create pattern: fetch by guide,
/// create through the factory when absent — the unique guide index arbitrates a race.
/// </summary>
public interface IGuideAnalyticsRepository
{
    Task<GuideAnalytics?> GetByGuideAsync(
        GuideId guideId, CancellationToken cancellationToken = default);

    Task AddAsync(GuideAnalytics analytics, CancellationToken cancellationToken = default);
}
