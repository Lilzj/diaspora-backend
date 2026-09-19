namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// The permission vocabulary authorisation checks are written against.
/// <para>
/// Handlers and endpoints name permissions, never roles. Roles are an assignment
/// mechanism; permissions are the thing being checked. Keeping that separation means a
/// new role is a mapping change rather than a hunt through the codebase for
/// <c>role == "Moderator"</c>.
/// </para>
/// </summary>
public static class IdentityPermissions
{
    // ─── Self-service ───────────────────────────────────────────────────
    public const string ReadOwnProfile = "identity:profile:read";
    public const string UpdateOwnProfile = "identity:profile:update";
    public const string ChangeOwnPassword = "identity:password:change";
    public const string ReadOwnSessions = "identity:sessions:read";
    public const string RevokeOwnSessions = "identity:sessions:revoke";
    public const string ManageOwnDevices = "identity:devices:manage";
    public const string ReadOwnSecurityEvents = "identity:security-events:read";
    public const string SubmitVerification = "identity:verification:submit";

    // No contributor permissions here. B2 declared content:publish, messaging:send,
    // jobs:apply and organizations:create in this class for modules that did not exist.
    // Those belong to the modules that enforce them — they now reach the catalogue through
    // each module's own IPermissionContributor, and Identity owns none of them. This is the
    // literal B2.6 rule: Identity must never contain Marketplace, Messaging or Job
    // permissions.

    // ─── Moderation ─────────────────────────────────────────────────────
    public const string ReadAnyUser = "identity:users:read";
    public const string SuspendUser = "identity:users:suspend";
    public const string ReinstateUser = "identity:users:reinstate";
    public const string ReadAnySecurityEvents = "identity:security-events:read-any";

    // ─── Administration ─────────────────────────────────────────────────
    public const string DisableUser = "identity:users:disable";
    public const string RevokeAnySession = "identity:sessions:revoke-any";
    public const string AssignRoles = "identity:roles:assign";

    /// <summary>
    /// Re-announce a fact Identity already holds, for a member whose downstream mirror is stale.
    /// <para>
    /// It changes nothing here. Identity stays the owner of the fact; this only says it again,
    /// for accounts whose phone was verified before anything published the event.
    /// </para>
    /// </summary>
    public const string RepublishMemberFacts = "identity:users:republish-facts";

    // ─── Machine-to-machine ─────────────────────────────────────────────

    /// <summary>
    /// Held only by the Trust &amp; Verification Engine. The single permission that can
    /// move a member to Verified.
    /// </summary>
    public const string ApplyTrustDecision = "identity:trust:apply-decision";
}
