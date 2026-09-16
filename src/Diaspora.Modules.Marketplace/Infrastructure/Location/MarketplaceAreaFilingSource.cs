using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Infrastructure.Location;

/// <summary>
/// Files the marketplace listings written before filing existed, through the same service the
/// create handler now uses — so a backfilled listing lands exactly where a new one would.
/// </summary>
public sealed class MarketplaceAreaFilingSource : IAreaFilingSource
{
    private readonly DiasporaDbContext _dbContext;
    private readonly ListingAreaFiling _filing;
    private readonly IUnitOfWork _unitOfWork;

    public MarketplaceAreaFilingSource(
        DiasporaDbContext dbContext, ListingAreaFiling filing, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _filing = filing;
        _unitOfWork = unitOfWork;
    }

    public string ResourceType => MarketplaceResourceCounter.Key;

    public async Task<AreaFilingBatch> FileUnplacedAsync(
        int skip, int limit, CancellationToken cancellationToken = default)
    {
        var unfiled = await _dbContext.Set<MarketplaceListing>()
            .Where(listing => listing.Location.AreaPath == null)
            .OrderBy(listing => listing.Id)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(limit, 1))
            .ToListAsync(cancellationToken);

        var filed = 0;

        foreach (var listing in unfiled)
        {
            var placed = await _filing.FileAsync(listing.Location, cancellationToken);

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
