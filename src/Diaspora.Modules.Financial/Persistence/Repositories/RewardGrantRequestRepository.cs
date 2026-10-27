using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for staff grants waiting for a second Admin.</summary>
public sealed class RewardGrantRequestRepository : IRewardGrantRequestRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RewardGrantRequestRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RewardGrantRequest> Requests => _dbContext.Set<RewardGrantRequest>();

    public void Add(RewardGrantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Requests.Add(request);
    }

    public void Update(RewardGrantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        FinancialAggregateUpdater.MarkDirty(_dbContext, request);
    }

    public async Task<RewardGrantRequest?> FindAsync(
        RewardGrantRequestId requestId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestId);

        return await Requests.FirstOrDefaultAsync(request => request.Id == requestId, cancellationToken);
    }

    public async Task<RewardGrantRequest?> FindByReferenceAsync(
        string grantReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(grantReference))
        {
            return null;
        }

        var normalized = grantReference.Trim();

        return Requests.Local.FirstOrDefault(request =>
                string.Equals(request.GrantReference, normalized, StringComparison.Ordinal))
            ?? await Requests.FirstOrDefaultAsync(
                request => request.GrantReference == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<RewardGrantRequest>> ListAsync(
        RewardGrantRequestStatus? status,
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
            var excluded = excludeIds.Select(RewardGrantRequestId.From).ToList();

            requests = requests.Where(request => !excluded.Contains(request.Id));
        }

        return await requests
            .OrderByDescending(request => request.RequestedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }
}
