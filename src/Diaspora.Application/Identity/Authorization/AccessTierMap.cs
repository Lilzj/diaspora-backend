using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// Projects Identity's nine-state access ladder onto the platform's five coarse tiers.
/// <para>
/// This is the whole of the coupling between Identity and every other module's
/// authorisation rules — one function, in one file, that Identity owns. A Marketplace
/// module writes <c>AccessTier.Verified</c> and never learns that
/// <c>VerificationUnderReview</c> exists; Identity can add a tenth state tomorrow and, so
/// long as it lands on an existing tier, nothing downstream changes.
/// </para>
/// <para>
/// Restricted states keep a nominal tier and set the restriction flag instead of
/// collapsing to <see cref="AccessTier.Anonymous"/>. A suspended member is still a member
/// — they can reach the appeal screen — they simply hold nothing.
/// </para>
/// </summary>
public static class AccessTierMap
{
    public static AccessTier ToTier(AccessState accessState)
    {
        ArgumentNullException.ThrowIfNull(accessState);

        if (accessState == AccessState.RegistrationStarted ||
            accessState == AccessState.PhoneVerificationPending)
        {
            return AccessTier.Onboarding;
        }

        if (accessState == AccessState.LimitedAccess)
        {
            return AccessTier.Limited;
        }

        if (accessState == AccessState.VerificationPending ||
            accessState == AccessState.VerificationUnderReview)
        {
            return AccessTier.PendingVerification;
        }

        if (accessState == AccessState.Verified)
        {
            return AccessTier.Verified;
        }

        // Suspended, Locked, Disabled. The tier is not consulted for a restricted subject,
        // but reporting Limited rather than Anonymous keeps "signed in but restricted"
        // distinguishable from "not signed in" anywhere the tier is displayed.
        return AccessTier.Limited;
    }

    public static AuthorizationSubject ToSubject(AccessState accessState, UserRole role)
    {
        ArgumentNullException.ThrowIfNull(accessState);
        ArgumentNullException.ThrowIfNull(role);

        return new AuthorizationSubject(
            ToTier(accessState),
            role.Name,
            role.IsStaff,
            accessState.IsRestricted);
    }

    public static AuthorizationSubject ToSubject(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return ToSubject(user.AccessState, user.Role);
    }
}
