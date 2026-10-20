using Diaspora.Contracts.Financial.V1.Entitlements;
using Diaspora.Modules.Financial.Domain.Subscriptions;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// The entitlement keys a member holds, as other modules may read them.
/// <para>
/// The same rule as <see cref="EntitlementResolver"/>: the active subscription's own entitlement rows,
/// and nothing from a subscription whose status no longer grants access.
/// </para>
/// </summary>
public sealed class MemberEntitlementReader : IMemberEntitlementReader
{
    private readonly ISubscriptionRepository _subscriptions;

    public MemberEntitlementReader(ISubscriptionRepository subscriptions) =>
        _subscriptions = subscriptions;

    public async Task<IReadOnlySet<string>> GrantedKeysAsync(
        Guid memberUserId, CancellationToken cancellationToken = default)
    {
        if (memberUserId == Guid.Empty)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var subscription = await _subscriptions
            .FindActiveForSubscriberAsync(memberUserId, cancellationToken)
            .ConfigureAwait(false);

        return subscription is { GrantsAccess: true }
            ? subscription.Entitlements
                .Select(entitlement => entitlement.Key)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }
}
