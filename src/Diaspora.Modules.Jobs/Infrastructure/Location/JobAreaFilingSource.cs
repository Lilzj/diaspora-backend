using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Infrastructure.Location;

/// <summary>
/// Files the job postings written before filing existed — on 16 Sep 2026, every one of them —
/// through the same service the create and update handlers now use.
/// </summary>
public sealed class JobAreaFilingSource : IAreaFilingSource
{
    private readonly DiasporaDbContext _dbContext;
    private readonly JobAreaFiling _filing;
    private readonly IUnitOfWork _unitOfWork;

    public JobAreaFilingSource(
        DiasporaDbContext dbContext, JobAreaFiling filing, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _filing = filing;
        _unitOfWork = unitOfWork;
    }

    public string ResourceType => JobResourceCounter.Key;

    public async Task<AreaFilingBatch> FileUnplacedAsync(
        int skip, int limit, CancellationToken cancellationToken = default)
    {
        var unfiled = await _dbContext.Set<JobPosting>()
            .Where(posting => posting.Location.AreaPath == null)
            .OrderBy(posting => posting.Id)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(limit, 1))
            .ToListAsync(cancellationToken);

        var filed = 0;

        foreach (var posting in unfiled)
        {
            var placed = await _filing.FileAsync(posting.Location, cancellationToken);

            if (posting.FileUnderArea(placed))
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
