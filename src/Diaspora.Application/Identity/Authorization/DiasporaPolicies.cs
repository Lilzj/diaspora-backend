using Diaspora.Contracts.Authorization;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// The authorisation policy names Identity's endpoints reference.
/// <para>
/// Moved out of the API project in B2.6 so the policy <em>contributor</em> can sit beside
/// the permission and feature contributors that define what the policies mean. Controllers
/// keep referencing the same constants; only the namespace changed.
/// </para>
/// <para>
/// Two vocabularies on purpose. A policy name is dotted (<c>identity.profile.read</c>) and
/// is what an endpoint attribute says; a permission is colon-separated
/// (<c>identity:profile:read</c>) and is what the caller actually holds. Keeping them
/// visibly different means a reader can tell at a glance which layer a string belongs to,
/// and it leaves room for a policy whose rule is not a single permission — the two tier
/// policies at the bottom are exactly that.
/// </para>
/// </summary>
public static class DiasporaPolicies
{
    // ─── Permission-backed ──────────────────────────────────────────────
    public const string ReadOwnProfile = "identity.profile.read";
    public const string UpdateOwnProfile = "identity.profile.update";
    public const string ChangeOwnPassword = "identity.password.change";
    public const string ReadOwnSessions = "identity.sessions.read";
    public const string RevokeOwnSessions = "identity.sessions.revoke";
    public const string ManageOwnDevices = "identity.devices.manage";

    /// <summary>
    /// Kebab-cased to match its permission (<c>identity:security-events:read</c>). B2 had
    /// this one camel-cased, which made it the only name in the platform that did not
    /// mechanically follow from its permission.
    /// </summary>
    public const string ReadOwnSecurityEvents = "identity.security-events.read";

    public const string SubmitVerification = "identity.verification.submit";
    public const string SuspendUsers = "identity.users.suspend";
    public const string ReinstateUsers = "identity.users.reinstate";
    public const string DisableUsers = "identity.users.disable";
    public const string ApplyTrustDecision = "identity.trust.apply-decision";

    /// <summary>
    /// The four staff policies added on 19 Sep 2026.
    /// <para>
    /// Their permissions were declared from the beginning and no policy ever carried them,
    /// so no route could require them and nothing could call them. Reading a member,
    /// reading their history, signing them out and giving them a role were all things the
    /// platform said staff could do and gave them no way to do — which is why every staff
    /// account so far was made with a database write.
    /// </para>
    /// </summary>
    public const string ReadAnyUser = "identity.users.read-any";

    public const string ReadAnySecurityEvents = "identity.security-events.read-any";

    public const string RevokeAnySession = "identity.sessions.revoke-any";

    public const string AssignRoles = "identity.roles.assign";

    /// <summary>Re-announce a member fact whose downstream mirror is stale.</summary>
    public const string RepublishMemberFacts = "identity.users.republish-facts";

    // ─── Tier-backed ────────────────────────────────────────────────────

    /// <summary>Requires phone verification — the floor for anything beyond onboarding.</summary>
    public const string RequireOnboarded = "identity.state.onboarded";

    /// <summary>Requires Diaspora verification. The gate on every contributor action.</summary>
    public const string RequireVerified = "identity.state.verified";

    /// <summary>
    /// The permission behind each permission-backed policy. Exposed so the contributor and
    /// the architecture test that checks the two stay aligned read from one source.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PermissionPolicies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ReadOwnProfile] = IdentityPermissions.ReadOwnProfile,
            [UpdateOwnProfile] = IdentityPermissions.UpdateOwnProfile,
            [ChangeOwnPassword] = IdentityPermissions.ChangeOwnPassword,
            [ReadOwnSessions] = IdentityPermissions.ReadOwnSessions,
            [RevokeOwnSessions] = IdentityPermissions.RevokeOwnSessions,
            [ManageOwnDevices] = IdentityPermissions.ManageOwnDevices,
            [ReadOwnSecurityEvents] = IdentityPermissions.ReadOwnSecurityEvents,
            [SubmitVerification] = IdentityPermissions.SubmitVerification,
            [SuspendUsers] = IdentityPermissions.SuspendUser,
            [ReinstateUsers] = IdentityPermissions.ReinstateUser,
            [DisableUsers] = IdentityPermissions.DisableUser,
            [ApplyTrustDecision] = IdentityPermissions.ApplyTrustDecision,
            [ReadAnyUser] = IdentityPermissions.ReadAnyUser,
            [ReadAnySecurityEvents] = IdentityPermissions.ReadAnySecurityEvents,
            [RevokeAnySession] = IdentityPermissions.RevokeAnySession,
            [AssignRoles] = IdentityPermissions.AssignRoles,
            [RepublishMemberFacts] = IdentityPermissions.RepublishMemberFacts
        };

    public static readonly IReadOnlyDictionary<string, AccessTier> TierPolicies =
        new Dictionary<string, AccessTier>(StringComparer.Ordinal)
        {
            [RequireOnboarded] = AccessTier.Limited,
            [RequireVerified] = AccessTier.Verified
        };
}
