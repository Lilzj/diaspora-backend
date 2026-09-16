using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Infrastructure.Location;

/// <summary>
/// Files the properties written before filing existed.
/// <para>
/// Every listing on production on 16 Sep 2026 — two homes, in Rose Hill and Quatre Bornes — was
/// filed under no area, so the map's cluster endpoint counted neither. This is how they, and any
/// property an older client writes unfiled later, are filed: through the same resolver the create
/// handler uses, so a backfilled home lands exactly where a newly listed one would.
/// </para>
/// </summary>
public sealed class HousingAreaFilingSource : IAreaFilingSource
{
    private readonly DiasporaDbContext _dbContext;
    private readonly HousingNeighborhoodResolver _neighborhoods;
    private readonly IUnitOfWork _unitOfWork;

    public HousingAreaFilingSource(
        DiasporaDbContext dbContext,
        HousingNeighborhoodResolver neighborhoods,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _neighborhoods = neighborhoods;
        _unitOfWork = unitOfWork;
    }

    public string ResourceType => HousingResourceCounter.Key;

    public async Task<AreaFilingBatch> FileUnplacedAsync(
        int skip, int limit, CancellationToken cancellationToken = default)
    {
        var unfiled = await _dbContext.Set<HousingListing>()
            .Where(listing => listing.Location.AreaPath == null)
            .OrderBy(listing => listing.Id)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(limit, 1))
            .ToListAsync(cancellationToken);

        var filed = 0;

        foreach (var listing in unfiled)
        {
            var placed = await _neighborhoods.FileUnderAreaAsync(
                listing.Location,
                listing.Address.Latitude,
                listing.Address.Longitude,
                cancellationToken);

            if (listing.FileUnderArea(placed))
            {
                filed++;
            }
        }

        if (filed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new AreaFilingBatch(unfiled.Count, filed);
    }
}
