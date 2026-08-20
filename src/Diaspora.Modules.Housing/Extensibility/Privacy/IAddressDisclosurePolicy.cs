using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Extensibility.Privacy;

/// <summary>
/// Implemented to narrow — never widen — what of an address a viewer may see.
/// <para>
/// <b>A policy may only take disclosure away.</b> The shipped rule is the landlord's own
/// <c>AddressVisibility</c> setting plus the confirmed-viewing unlock, and a contributor cannot
/// override it upward: the composer takes the most restrictive answer of every registered policy
/// and the listing's own setting. That direction is not negotiable, because the failure mode of a
/// policy that could widen is publishing somebody's home address, and a bug in a contributed class
/// must not be able to do that.
/// </para>
/// <para>
/// <b>What it exists for is jurisdiction.</b> A deployment operating where publishing a residential
/// address without consent is regulated, or where a particular city's rules are stricter than the
/// platform's default, ships a policy and the whole module honours it. Housing does not learn which
/// jurisdictions those are.
/// </para>
/// </summary>
public interface IAddressDisclosurePolicy
{
    /// <summary>What this policy calls itself, for the audit trail when it withholds something.</summary>
    string Name { get; }

    /// <summary>
    /// Whether this policy permits full disclosure to this viewer.
    /// <para>
    /// Returning false narrows to neighbourhood level. Returning true asserts nothing — the
    /// listing's own setting and every other policy still apply.
    /// </para>
    /// </summary>
    Task<bool> PermitsFullDisclosureAsync(
        HousingListing listing,
        UserId? viewerId,
        bool isOwner,
        bool isStaff,
        CancellationToken cancellationToken = default);
}
