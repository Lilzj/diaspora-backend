using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for subscriptions. Every read includes the entitlements.</summary>
public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SubscriptionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<Subscription> Subscriptions => _dbContext.Set<Subscription>()
        .Include(subscription => subscription.Entitlements);

    public void Add(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        _dbContext.Set<Subscription>().Add(subscription);
    }

    public void Update(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        FinancialAggregateUpdater.MarkDirty(_dbContext, subscription);
    }

    public async Task<Subscription?> FindAsync(
        SubscriptionId subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscriptionId);

        return await Subscriptions.FirstOrDefaultAsync(
            subscription => subscription.Id == subscriptionId, cancellationToken);
    }

    public async Task<Subscription?> FindActiveForSubscriberAsync(
        Guid subscriberUserId, CancellationToken cancellationToken = default) =>
        await Subscriptions
            .Where(subscription => subscription.SubscriberUserId == subscriberUserId
                && (subscription.Status == SubscriptionStatus.Active
                    || subscription.Status == SubscriptionStatus.Trial
                    || subscription.Status == SubscriptionStatus.GracePeriod))
            .OrderByDescending(subscription => subscription.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListForSubscriberAsync(
        Guid subscriberUserId, CancellationToken cancellationToken = default) =>
        await Subscriptions
            .Where(subscription => subscription.SubscriberUserId == subscriberUserId)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListDueForRenewalAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Subscriptions
            .Where(subscription =>
                (subscription.Status == SubscriptionStatus.Active
                    || subscription.Status == SubscriptionStatus.Trial)
                && subscription.CurrentPeriod.EndsAt != null
                && subscription.CurrentPeriod.EndsAt <= asOf)
            .OrderBy(subscription => subscription.CurrentPeriod.EndsAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Subscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.GracePeriod
                && subscription.GraceEndsAt != null
                && subscription.GraceEndsAt <= asOf)
            .OrderBy(subscription => subscription.GraceEndsAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListExpiringAsync(
        DateTimeOffset asOf,
        DateTimeOffset until,
        int take,
        CancellationToken cancellationToken = default) =>
        await Subscriptions
            .Where(subscription =>
                (subscription.Status == SubscriptionStatus.Active
                    || subscription.Status == SubscriptionStatus.Trial)
                && subscription.CurrentPeriod.EndsAt != null
                && subscription.CurrentPeriod.EndsAt > asOf
                && subscription.CurrentPeriod.EndsAt <= until
                && (subscription.ExpiryAnnouncedFor == null
                    || subscription.ExpiryAnnouncedFor != subscription.CurrentPeriod.EndsAt))
            .OrderBy(subscription => subscription.CurrentPeriod.EndsAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public Task<bool> HasEverSubscribedAsync(
        Guid subscriberUserId, string planCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            return Task.FromResult(false);
        }

        var normalized = planCode.Trim().ToLowerInvariant();

        return _dbContext.Set<Subscription>().AnyAsync(
            subscription => subscription.SubscriberUserId == subscriberUserId
                && subscription.PlanCode == normalized,
            cancellationToken);
    }

    public Task<int> CountByStatusAsync(
        SubscriptionStatus status, CancellationToken cancellationToken = default) =>
        _dbContext.Set<Subscription>().CountAsync(
            subscription => subscription.Status == status, cancellationToken);
}
