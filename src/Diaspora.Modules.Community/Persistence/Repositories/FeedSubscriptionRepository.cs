using Diaspora.Modules.Community.Domain.Feeds;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for saved feed views.</summary>
public sealed class FeedSubscriptionRepository : IFeedSubscriptionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public FeedSubscriptionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<FeedSubscription> Subscriptions => _dbContext.Set<FeedSubscription>();

    public async Task<FeedSubscription?> GetAsync(
        FeedSubscriptionId id, CancellationToken cancellationToken = default) =>
        await Subscriptions.FirstOrDefaultAsync(
            subscription => subscription.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FeedSubscription>> ListForMemberAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Subscriptions
            .Where(subscription => subscription.UserId == userId)
            .OrderBy(subscription => subscription.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        FeedSubscription subscription, CancellationToken cancellationToken = default) =>
        await Subscriptions.AddAsync(subscription, cancellationToken);

    public void Remove(FeedSubscription subscription) => Subscriptions.Remove(subscription);
}
