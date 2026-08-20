using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.AccessStates;

/// <summary>
/// The one and only declaration of which progressive-access transitions are legal.
/// Every state change in the platform is checked against this table, so the ladder
/// cannot be bypassed by a handler that forgets a guard.
/// </summary>
public static class AccessStateTransitions
{
    private static readonly Dictionary<int, AccessState[]> Map = new()
    {
        [AccessState.RegistrationStarted.Id] =
        [
            AccessState.PhoneVerificationPending,
            AccessState.Disabled
        ],

        [AccessState.PhoneVerificationPending.Id] =
        [
            AccessState.LimitedAccess,
            AccessState.Locked,
            AccessState.Disabled
        ],

        [AccessState.LimitedAccess.Id] =
        [
            AccessState.VerificationPending,
            AccessState.Suspended,
            AccessState.Locked,
            AccessState.Disabled
        ],

        [AccessState.VerificationPending.Id] =
        [
            AccessState.VerificationUnderReview,
            // Withdrawn by the member, or rejected before review began.
            AccessState.LimitedAccess,
            AccessState.Suspended,
            AccessState.Locked,
            AccessState.Disabled
        ],

        [AccessState.VerificationUnderReview.Id] =
        [
            AccessState.Verified,
            // Rejected by the Trust Engine — the member falls back to limited access.
            AccessState.LimitedAccess,
            AccessState.Suspended,
            AccessState.Locked,
            AccessState.Disabled
        ],

        [AccessState.Verified.Id] =
        [
            // The Trust Engine can revoke verification at any time.
            AccessState.LimitedAccess,
            AccessState.Suspended,
            AccessState.Locked,
            AccessState.Disabled
        ],

        [AccessState.Suspended.Id] =
        [
            // Reinstatement returns the member to whichever rung they were on.
            AccessState.LimitedAccess,
            AccessState.VerificationPending,
            AccessState.VerificationUnderReview,
            AccessState.Verified,
            AccessState.Locked,
            AccessState.Disabled
        ],

        [AccessState.Locked.Id] =
        [
            // Successful account recovery restores the previous rung.
            AccessState.PhoneVerificationPending,
            AccessState.LimitedAccess,
            AccessState.VerificationPending,
            AccessState.VerificationUnderReview,
            AccessState.Verified,
            AccessState.Disabled
        ],

        [AccessState.Disabled.Id] =
        [
            // Reactivation always re-enters at limited access; trust must be re-earned.
            AccessState.LimitedAccess
        ]
    };

    public static bool IsAllowed(AccessState from, AccessState to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (from == to) return false;

        return Map.TryGetValue(from.Id, out var targets) && targets.Contains(to);
    }

    public static IReadOnlyCollection<AccessState> AllowedFrom(AccessState from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return Map.TryGetValue(from.Id, out var targets) ? targets : [];
    }

    /// <summary>
    /// Every legal edge, for documentation generation and architecture tests.
    /// </summary>
    public static IReadOnlyCollection<AccessStateTransition> All =>
        Map.SelectMany(
                entry => entry.Value,
                (entry, target) => new AccessStateTransition(
                    Enumeration.FromId<AccessState>(entry.Key),
                    target))
            .ToArray();
}
