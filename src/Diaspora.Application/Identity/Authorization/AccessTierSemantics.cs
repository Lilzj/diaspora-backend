using Diaspora.Contracts.Authorization;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// The one authoritative reading of every <see cref="AccessTier"/>.
/// <para>
/// Before B2.0.1 the tiers had documentation but no <em>specification</em>: nothing said which
/// classes of capability a tier admits, so two modules could hold incompatible readings of the
/// same rung and both look correct in isolation. This type is that specification, expressed as
/// code so it can be asserted rather than remembered.
/// </para>
/// <para>
/// <b>The ladder is progression, not privilege.</b> A tier records how far a member has got
/// through Identity's access-state machine. Elevated authority — moderation, administration, a
/// service provider's own listing — is carried by <b>roles</b>, on a separate axis, and the two
/// must not be conflated. That is why there is no "Administrator" tier: a moderator at
/// <see cref="AccessTier.Limited"/> is a real and correct subject.
/// </para>
/// <para>
/// Nothing here changes behaviour. Every rule states what the platform already does; the value
/// is that it now says so in one place, and that
/// <c>AccessTierSemanticsArchitectureTests</c> fails the build when a module drifts from it.
/// </para>
/// </summary>
public static class AccessTierSemantics
{
    /// <summary>
    /// Which categories of capability a tier admits, ignoring role scope.
    /// <para>
    /// <see cref="CapabilityCategory.Contributor"/> and
    /// <see cref="CapabilityCategory.Administrative"/> are deliberately absent from every set:
    /// they are granted by role, at whatever tier their own definition names, and a tier never
    /// admits them on its own. Asking "does Limited admit administrative capabilities" is the
    /// conflation this patch exists to remove — the answer is that the question is malformed.
    /// </para>
    /// </summary>
    public static IReadOnlySet<CapabilityCategory> AdmissibleCategories(AccessTier tier) =>
        tier switch
        {
            // Nothing. An unauthenticated caller holds no capability of any category — public
            // routes are public because they require no permission at all, not because a gate
            // opened for them.
            AccessTier.Anonymous => new HashSet<CapabilityCategory>(),

            // Onboarding capabilities only. This is the rung the patch exists to pin down.
            AccessTier.Onboarding => new HashSet<CapabilityCategory>
            {
                CapabilityCategory.Onboarding
            },

            // The first rung of membership. Everything an onboarding subject could do, plus
            // ordinary participation.
            AccessTier.Limited => new HashSet<CapabilityCategory>
            {
                CapabilityCategory.Onboarding,
                CapabilityCategory.Member
            },

            // Identical to Limited by design. Waiting on the Trust Engine is not a demotion and
            // not a promotion; capabilities that stop applying once a request is in flight say
            // so with MaximumTier, which is the mechanism that already exists for it.
            AccessTier.PendingVerification => new HashSet<CapabilityCategory>
            {
                CapabilityCategory.Onboarding,
                CapabilityCategory.Member
            },

            // Full membership.
            AccessTier.Verified => new HashSet<CapabilityCategory>
            {
                CapabilityCategory.Onboarding,
                CapabilityCategory.Member,
                CapabilityCategory.Trusted
            },

            _ => new HashSet<CapabilityCategory>()
        };

    /// <summary>
    /// Whether a tier admits a category on progression alone.
    /// <para>
    /// Always false for the two role-scoped categories, whatever the tier. A caller asking
    /// whether a moderator may reach a moderation queue is asking about a role, and should ask
    /// the permission catalogue.
    /// </para>
    /// </summary>
    public static bool Admits(AccessTier tier, CapabilityCategory category) =>
        AdmissibleCategories(tier).Contains(category);

    /// <summary>
    /// The lowest tier at which a category's capabilities may open.
    /// <para>
    /// The inverse of <see cref="AdmissibleCategories"/>, and what the architecture test checks
    /// a declaration against. Null for the role-scoped categories: a contributor or
    /// administrative capability may sit at any tier, because its gate is the role.
    /// </para>
    /// </summary>
    public static AccessTier? FloorFor(CapabilityCategory category) => category switch
    {
        CapabilityCategory.Onboarding => AccessTier.Onboarding,
        CapabilityCategory.Member => AccessTier.Limited,
        CapabilityCategory.Trusted => AccessTier.Verified,
        _ => null
    };

    /// <summary>
    /// The highest tier at which a category's capabilities may open.
    /// <para>
    /// <see cref="CapabilityCategory.Member"/> stretches across two rungs — a member capability
    /// may name <see cref="AccessTier.Limited"/> or <see cref="AccessTier.PendingVerification"/>
    /// — because a capability that only makes sense once verification is in flight is still an
    /// ordinary member capability.
    /// </para>
    /// </summary>
    public static AccessTier? CeilingFor(CapabilityCategory category) => category switch
    {
        CapabilityCategory.Onboarding => AccessTier.Onboarding,
        CapabilityCategory.Member => AccessTier.PendingVerification,
        CapabilityCategory.Trusted => AccessTier.Verified,
        _ => null
    };

    /// <summary>
    /// Who sits at a tier, in one sentence. Surfaced in documentation and diagnostics.
    /// </summary>
    public static string Describe(AccessTier tier) => tier switch
    {
        AccessTier.Anonymous =>
            "No authenticated subject. Holds nothing.",

        AccessTier.Onboarding =>
            "An account exists but onboarding is not finished — the phone number is unverified. "
            + "Holds only capabilities whose purpose is to finish onboarding.",

        AccessTier.Limited =>
            "Onboarding is complete. The first tier of community participation: read, take "
            + "part, manage your own account, ask to be verified.",

        AccessTier.PendingVerification =>
            "A verification request is with the Trust Engine. Holds exactly what Limited holds; "
            + "capabilities that only make sense before submitting close via MaximumTier.",

        AccessTier.Verified =>
            "Trust has granted verification. Full membership, including the capabilities whose "
            + "cost of misuse falls on other people.",

        _ => "Unknown tier."
    };

    /// <summary>
    /// What moves a subject off a tier. Progression only — restriction is a separate axis.
    /// <para>
    /// Restricted states (suspended, locked, disabled) keep a nominal tier and set the
    /// restriction flag instead of collapsing to <see cref="AccessTier.Anonymous"/>, so a
    /// suspended member can still reach the screen that explains the suspension. See
    /// <see cref="AccessTierMap"/>.
    /// </para>
    /// </summary>
    public static string DescribeTransition(AccessTier tier) => tier switch
    {
        AccessTier.Anonymous => "Registering creates an account and lands on Onboarding.",

        AccessTier.Onboarding => "Verifying the phone number lands on Limited.",

        AccessTier.Limited =>
            "Submitting a verification request lands on PendingVerification.",

        AccessTier.PendingVerification =>
            "A granted Trust decision lands on Verified; a refusal returns to Limited.",

        AccessTier.Verified => "Terminal for progression. Revocation returns to Limited.",

        _ => "Unknown tier."
    };
}
