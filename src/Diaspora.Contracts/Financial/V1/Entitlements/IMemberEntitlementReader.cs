namespace Diaspora.Contracts.Financial.V1.Entitlements;

/// <summary>
/// Which entitlement keys a member's subscription grants right now, for modules that gate on one.
/// <para>
/// <b>Why a seam.</b> Entitlements belong to Financial: a plan names them, and they are copied onto
/// the subscription at activation. Analytics gates its premium summary on <c>analytics.advanced</c>
/// and composes after Financial without referencing it, so it asks here. Financial implements this
/// from the same active-subscription rule its own <c>/entitlements/me</c> uses — an ended
/// subscription grants nothing, whatever rows it still carries.
/// </para>
/// </summary>
public interface IMemberEntitlementReader
{
    /// <summary>
    /// The keys the member's active subscription grants, lower case and dotted. Empty when they have
    /// no subscription that grants access.
    /// </summary>
    Task<IReadOnlySet<string>> GrantedKeysAsync(
        Guid memberUserId, CancellationToken cancellationToken = default);
}
