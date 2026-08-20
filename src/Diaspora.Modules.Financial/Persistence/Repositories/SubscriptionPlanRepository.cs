using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for the plan catalogue. Every read includes the entitlements.</summary>
public sealed class SubscriptionPlanRepository : ISubscriptionPlanRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SubscriptionPlanRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<SubscriptionPlan> Plans => _dbContext.Set<SubscriptionPlan>()
        .Include(plan => plan.Entitlements);

    public void Add(SubscriptionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        _dbContext.Set<SubscriptionPlan>().Add(plan);
    }

    public void Update(SubscriptionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        FinancialAggregateUpdater.MarkDirty(_dbContext, plan);
    }

    public async Task<SubscriptionPlan?> FindAsync(
        SubscriptionPlanId planId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planId);

        return await Plans.FirstOrDefaultAsync(plan => plan.Id == planId, cancellationToken);
    }

    public async Task<SubscriptionPlan?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToLowerInvariant();

        return await Plans.FirstOrDefaultAsync(plan => plan.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> ListActiveAsync(
        FinancialAccountType? audienceType,
        string? countryCode,
        CancellationToken cancellationToken = default)
    {
        var plans = Plans.Where(plan => plan.IsActive);

        if (audienceType is { } audience)
        {
            plans = plans.Where(plan => plan.AudienceType == audience);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();

            // A plan with no country is offered everywhere; one with a country only there.
            plans = plans.Where(plan => plan.CountryCode == null || plan.CountryCode == country);
        }

        return await plans
            .OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Plans
            .OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalized = code.Trim().ToLowerInvariant();

        return _dbContext.Set<SubscriptionPlan>().AnyAsync(
            plan => plan.Code == normalized, cancellationToken);
    }
}
