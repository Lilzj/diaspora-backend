using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for the versioned policy catalogue.
/// <para>
/// Every read includes the parameters, because a policy without them cannot answer a price — and a
/// policy that resolves and then cannot answer is worse than one that does not resolve.
/// </para>
/// </summary>
public sealed class FinancialPolicyRepository : IFinancialPolicyRepository
{
    private readonly DiasporaDbContext _dbContext;

    public FinancialPolicyRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<FinancialPolicy> Policies => _dbContext.Set<FinancialPolicy>()
        .Include(policy => policy.Parameters);

    public void Add(FinancialPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _dbContext.Set<FinancialPolicy>().Add(policy);
    }

    public void Update(FinancialPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        FinancialAggregateUpdater.MarkDirty(_dbContext, policy);
    }

    public async Task<FinancialPolicy?> FindAsync(
        FinancialPolicyId policyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policyId);

        return await Policies.FirstOrDefaultAsync(policy => policy.Id == policyId, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialPolicy>> ListActiveByKeyAsync(
        string policyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return [];
        }

        var normalized = policyKey.Trim().ToLowerInvariant();

        return await Policies
            .Where(policy => policy.PolicyKey == normalized && policy.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialPolicy>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        await Policies
            .Where(policy => policy.IsActive)
            .OrderBy(policy => policy.PolicyKey)
            .ThenByDescending(policy => policy.Version)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FinancialPolicy>> ListByKeyAsync(
        string policyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return [];
        }

        var normalized = policyKey.Trim().ToLowerInvariant();

        return await Policies
            .Where(policy => policy.PolicyKey == normalized)
            .OrderByDescending(policy => policy.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialPolicy>> ListByKindAsync(
        FinancialPolicyKind kind, CancellationToken cancellationToken = default) =>
        await Policies
            .Where(policy => policy.Kind == kind)
            .OrderBy(policy => policy.PolicyKey)
            .ThenByDescending(policy => policy.Version)
            .ToListAsync(cancellationToken);

    public async Task<int> HighestVersionAsync(
        string policyKey,
        PolicyScopeKind scope,
        string? scopeValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return 0;
        }

        var normalizedKey = policyKey.Trim().ToLowerInvariant();
        var normalizedScope = Normalize(scope, scopeValue);

        var versions = await _dbContext.Set<FinancialPolicy>()
            .Where(policy => policy.PolicyKey == normalizedKey
                && policy.Scope == scope
                && policy.ScopeValue == normalizedScope)
            .Select(policy => policy.Version)
            .ToListAsync(cancellationToken);

        return versions.Count == 0 ? 0 : versions.Max();
    }

    public Task<bool> ExistsAsync(
        string policyKey,
        PolicyScopeKind scope,
        string? scopeValue,
        int version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return Task.FromResult(false);
        }

        var normalizedKey = policyKey.Trim().ToLowerInvariant();
        var normalizedScope = Normalize(scope, scopeValue);

        return _dbContext.Set<FinancialPolicy>().AnyAsync(
            policy => policy.PolicyKey == normalizedKey
                && policy.Scope == scope
                && policy.ScopeValue == normalizedScope
                && policy.Version == version,
            cancellationToken);
    }

    private static string? Normalize(PolicyScopeKind scope, string? scopeValue)
    {
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return null;
        }

        var trimmed = scopeValue.Trim();

        return scope == PolicyScopeKind.Country ? trimmed.ToUpperInvariant() : trimmed.ToLowerInvariant();
    }
}
