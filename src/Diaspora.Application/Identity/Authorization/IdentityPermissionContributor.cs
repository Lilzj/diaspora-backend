using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// Declares the permissions the Identity module owns — and only those.
/// <para>
/// B2 kept the entire platform's permission catalogue here, including
/// <c>messaging:send</c> and <c>jobs:apply</c> for modules that did not exist. Those have
/// moved to the modules that will enforce them. What is left is what Identity can
/// actually answer for: your profile, your password, your sessions, your devices, your
/// security history, and the moderation and administration actions taken against an
/// account.
/// </para>
/// </summary>
public sealed class IdentityPermissionContributor : IPermissionContributor
{
    private static readonly IReadOnlySet<string> ModeratorAndAbove =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(UserRole.Moderator),
            nameof(UserRole.Admin),
            nameof(UserRole.SuperAdmin)
        };

    private static readonly IReadOnlySet<string> AdminAndAbove =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(UserRole.Admin),
            nameof(UserRole.SuperAdmin)
        };

    public string Module => IdentityModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        // ─── Self-service ───────────────────────────────────────────────
        yield return new PermissionDefinition(
            IdentityPermissions.ReadOwnProfile, Module, "Read your own profile.")
        {
            // The one capability that outlives a restriction: the suspension screen has to
            // be able to address the member by name.
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            IdentityPermissions.UpdateOwnProfile, Module, "Edit your own profile.");

        yield return new PermissionDefinition(
            IdentityPermissions.ChangeOwnPassword, Module, "Change your own password.");

        yield return new PermissionDefinition(
            IdentityPermissions.ReadOwnSessions, Module, "List your own sessions.");

        yield return new PermissionDefinition(
            IdentityPermissions.RevokeOwnSessions, Module, "Sign your own devices out.");

        yield return new PermissionDefinition(
            IdentityPermissions.ManageOwnDevices, Module, "Register and manage your own devices.");

        yield return new PermissionDefinition(
            IdentityPermissions.ReadOwnSecurityEvents, Module, "Read your own security history.");

        yield return new PermissionDefinition(
            IdentityPermissions.SubmitVerification, Module, "Ask to be considered for verification.")
        {
            // Available at limited access and nowhere above it: once a request is with the
            // Trust Engine, submitting another is not something the member can do. The tier
            // ceiling expresses that without a special case in the resolver.
            MinimumTier = AccessTier.Limited,
            MaximumTier = AccessTier.Limited
        };

        // ─── Moderation ─────────────────────────────────────────────────
        yield return new PermissionDefinition(
            IdentityPermissions.ReadAnyUser, Module, "Read any member's account.")
        {
            Roles = ModeratorAndAbove
        };

        yield return new PermissionDefinition(
            IdentityPermissions.SuspendUser, Module, "Suspend an account.")
        {
            Roles = ModeratorAndAbove
        };

        yield return new PermissionDefinition(
            IdentityPermissions.ReinstateUser, Module, "Lift a suspension or hard lock.")
        {
            Roles = ModeratorAndAbove
        };

        yield return new PermissionDefinition(
            IdentityPermissions.ReadAnySecurityEvents, Module, "Read any member's security history.")
        {
            Roles = ModeratorAndAbove
        };

        // ─── Administration ─────────────────────────────────────────────
        yield return new PermissionDefinition(
            IdentityPermissions.DisableUser, Module, "Close an account.")
        {
            Roles = AdminAndAbove
        };

        yield return new PermissionDefinition(
            IdentityPermissions.RevokeAnySession, Module, "Sign any member out.")
        {
            Roles = AdminAndAbove
        };

        yield return new PermissionDefinition(
            IdentityPermissions.AssignRoles, Module, "Assign platform roles.")
        {
            Roles = AdminAndAbove
        };

        yield return new PermissionDefinition(
            IdentityPermissions.RepublishMemberFacts,
            Module,
            "Re-announce a member's verified phone, for a stale downstream mirror.")
        {
            Roles = AdminAndAbove
        };

        // ─── Machine-to-machine ─────────────────────────────────────────
        yield return new PermissionDefinition(
            IdentityPermissions.ApplyTrustDecision,
            Module,
            "Apply a verification verdict to a member's access state. Held only by the Trust Engine.")
        {
            // Service audience, so no tier and no role can resolve to it. This is ADR-002's
            // Identity/Trust separation expressed as data rather than as a comment.
            Audience = PermissionAudience.Service,
            MinimumTier = null
        };
    }
}
