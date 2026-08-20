using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Feeds;

/// <summary>Reads and writes saved feed views.</summary>
public interface IFeedSubscriptionRepository
{
    Task<FeedSubscription?> GetAsync(
        FeedSubscriptionId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedSubscription>> ListForMemberAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(FeedSubscription subscription, CancellationToken cancellationToken = default);

    void Remove(FeedSubscription subscription);
}
