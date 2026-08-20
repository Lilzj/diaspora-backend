using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for the policy catalogue.
/// <para>
/// Small enough to load whole. The enabled list is read on every generation through the cache, so
/// this is the miss path rather than the hot path — which is why it includes the parameters
/// unconditionally rather than trying to be clever about it.
/// </para>
/// </summary>
public sealed class RecommendationPolicyRepository : IRecommendationPolicyRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RecommendationPolicyRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RecommendationPolicy> Policies => _dbContext.Set<RecommendationPolicy>();

    public void Add(RecommendationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        Policies.Add(policy);
    }

    public async Task<RecommendationPolicy?> FindAsync(
        RecommendationPolicyId policyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policyId);

        return await Policies
            .Include(policy => policy.Parameters)
            .FirstOrDefaultAsync(policy => policy.Id == policyId, cancellationToken);
    }

    public async Task<RecommendationPolicy?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant();

        return await Policies
            .Include(policy => policy.Parameters)
            .FirstOrDefaultAsync(policy => policy.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<RecommendationPolicy>> ListEnabledAsync(
        CancellationToken cancellationToken = default) =>
        await Policies
            .Include(policy => policy.Parameters)
            .Where(policy => policy.IsEnabled)
            .OrderBy(policy => policy.Order)
            .ThenBy(policy => policy.Key)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecommendationPolicy>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Policies
            .Include(policy => policy.Parameters)
            .OrderBy(policy => policy.Order)
            .ThenBy(policy => policy.Key)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(false);
        }

        var normalized = key.Trim().ToLowerInvariant();

        return Policies.AnyAsync(policy => policy.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<RecommendationPolicy>> ListByKindAsync(
        RecommendationPolicyKind kind, CancellationToken cancellationToken = default) =>
        await Policies
            .Include(policy => policy.Parameters)
            .Where(policy => policy.Kind == kind)
            .OrderBy(policy => policy.Order)
            .ToListAsync(cancellationToken);

    public void Update(RecommendationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, policy);
    }
}
