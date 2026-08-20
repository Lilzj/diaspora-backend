using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Subscriptions;

/// <summary>Retrieval and staging for subscriptions.</summary>
public interface ISubscriptionRepository
{
    void Add(Subscription subscription);

    void Update(Subscription subscription);

    Task<Subscription?> FindAsync(
        SubscriptionId subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>The subscription currently granting access to a member, if any.</summary>
    Task<Subscription?> FindActiveForSubscriberAsync(
        Guid subscriberUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subscription>> ListForSubscriberAsync(
        Guid subscriberUserId, CancellationToken cancellationToken = default);

    /// <summary>Subscriptions whose period has ended. The renewal sweep's input.</summary>
    Task<IReadOnlyList<Subscription>> ListDueForRenewalAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);

    /// <summary>Subscriptions whose grace period has run out.</summary>
    Task<IReadOnlyList<Subscription>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscriptions whose period ends within a lead time and which have not been announced.
    /// <para>
    /// The announced filter is in the query rather than in the sweep, so a deployment with a hundred
    /// thousand subscribers does not load the ones it has already warned on every pass.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Subscription>> ListExpiringAsync(
        DateTimeOffset asOf,
        DateTimeOffset until,
        int take,
        CancellationToken cancellationToken = default);

    Task<bool> HasEverSubscribedAsync(
        Guid subscriberUserId, string planCode, CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(
        SubscriptionStatus status, CancellationToken cancellationToken = default);
}
