using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for credit grants waiting for a second Admin.</summary>
public sealed class CreditGrantRequestRepository : ICreditGrantRequestRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreditGrantRequestRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreditGrantRequest> Requests => _dbContext.Set<CreditGrantRequest>();

    public void Add(CreditGrantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Requests.Add(request);
    }

    public void Update(CreditGrantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        FinancialAggregateUpdater.MarkDirty(_dbContext, request);
    }

    public async Task<CreditGrantRequest?> FindAsync(
        CreditGrantRequestId requestId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestId);

        return await Requests.FirstOrDefaultAsync(request => request.Id == requestId, cancellationToken);
    }

    public async Task<IReadOnlyList<CreditGrantRequest>> ListAsync(
        CreditGrantRequestStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        var requests = Requests.AsNoTracking();

        if (status is { } wanted)
        {
            requests = requests.Where(request => request.Status == wanted);
        }

        if (before is { } boundary)
        {
            requests = requests.Where(request => request.RequestedAt <= boundary);
        }

        if (excludeIds is { Count: > 0 })
        {
            var excluded = excludeIds.Select(CreditGrantRequestId.From).ToList();

            requests = requests.Where(request => !excluded.Contains(request.Id));
        }

        return await requests
            .OrderByDescending(request => request.RequestedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }
}
