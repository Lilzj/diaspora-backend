using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Providers.Infrastructure.Location;

/// <summary>
/// Files the providers registered unfiled, through the same service registration now uses.
/// </summary>
public sealed class ProviderAreaFilingSource : IAreaFilingSource
{
    private readonly DiasporaDbContext _dbContext;
    private readonly ProviderAreaFiling _filing;
    private readonly IUnitOfWork _unitOfWork;

    public ProviderAreaFilingSource(
        DiasporaDbContext dbContext, ProviderAreaFiling filing, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _filing = filing;
        _unitOfWork = unitOfWork;
    }

    public string ResourceType => ProviderResourceCounter.Key;

    public async Task<AreaFilingBatch> FileUnplacedAsync(
        int skip, int limit, CancellationToken cancellationToken = default)
    {
        var unfiled = await _dbContext.Set<Provider>()
            .Where(provider => provider.Location.HierarchyPath == null)
            .OrderBy(provider => provider.Id)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(limit, 1))
            .ToListAsync(cancellationToken);

        var filed = 0;

        foreach (var provider in unfiled)
        {
            var placed = await _filing.FileAsync(provider.Location, cancellationToken: cancellationToken);

            if (provider.FileUnderArea(placed))
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
