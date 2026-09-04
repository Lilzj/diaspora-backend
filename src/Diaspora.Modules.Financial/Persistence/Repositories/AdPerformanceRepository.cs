using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for delivery counters.
/// <para>
/// <b>The lookup by tuple is the hot one.</b> It runs once per served promotion, on the path that
/// turns an integration event into a number, and it is indexed on exactly the four columns it
/// filters by — campaign, placement, surface, day. Anything less would put a scan on delivery.
/// </para>
/// </summary>
public sealed class AdPerformanceRepository : IAdPerformanceRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AdPerformanceRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AdPerformanceDay> Rows => _dbContext.Set<AdPerformanceDay>();

    public void Add(AdPerformanceDay row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Rows.Add(row);
    }

    public async Task<AdPerformanceDay?> FindAsync(
        AdCampaignId campaignId,
        PlacementId? placementId,
        string surface,
        DateOnly day,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaignId);

        return await Rows.FirstOrDefaultAsync(
            row => row.CampaignId == campaignId
                && row.PlacementId == placementId
                && row.Surface == surface
                && row.Day == day,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdPerformanceDay>> ListForCampaignAsync(
        AdCampaignId campaignId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaignId);

        return await Rows
            .AsNoTracking()
            .Where(row => row.CampaignId == campaignId && row.Day >= from && row.Day <= to)
            .OrderBy(row => row.Day)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdPerformanceDay>> ListForPlacementAsync(
        PlacementId placementId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placementId);

        return await Rows
            .AsNoTracking()
            .Where(row => row.PlacementId == placementId && row.Day >= from && row.Day <= to)
            .OrderBy(row => row.Day)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdPerformanceDay>> ListForCampaignsAsync(
        IReadOnlyCollection<AdCampaignId> campaignIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaignIds);

        if (campaignIds.Count == 0)
        {
            return [];
        }

        var ids = campaignIds.ToList();

        return await Rows
            .AsNoTracking()
            .Where(row => ids.Contains(row.CampaignId) && row.Day >= from && row.Day <= to)
            .OrderBy(row => row.Day)
            .ToListAsync(cancellationToken);
    }
}
