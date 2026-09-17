using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Community.Authorization;

/// <summary>
/// Declares the permissions the Community module owns.
/// <para>
/// The tier band encodes one product judgement repeatedly: <b>reading is cheap, writing is
/// social, posting is consequential.</b> Reading opens at <see cref="AccessTier.Onboarding"/>,
/// because a member who cannot see the community has no reason to finish verifying — the same
/// loop-with-no-entrance reasoning B5 applied to profile photographs.
/// </para>
/// <para>
/// <b>Posting opens at <see cref="AccessTier.Verified"/></b>, and it is the highest band in the
/// module. An unverified account posting a fabricated emergency into a neighbourhood feed is the
/// single worst thing this module can be used for, and it is worth being deliberate that
/// commenting and reacting sit a rung lower so a member can participate while their verification
/// is in flight.
/// </para>
/// <para>
/// Reporting survives restriction. A suspended member who witnesses something harmful must still
/// be able to say so, and removing that is the one restriction that makes the platform less safe
/// rather than more.
/// </para>
/// </summary>
public sealed class CommunityPermissionContributor : IPermissionContributor
{
    public string Module => CommunityModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            CommunityPermissions.Read, Module, "Read the community.")
        {
            MinimumTier = AccessTier.Limited,

            // A restricted member's screens still render. A feed that 403s looks like an outage.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Post, Module, "Post to the community.")
        {
            MinimumTier = AccessTier.Verified
        };

        // Emergency is its own permission, decided 16 Sep 2026: only verified members may publish
        // one. Posting at any urgency already needs a verified account, so today this changes
        // nothing a member can do — it makes the rule a named thing a client can be told about,
        // and one that can tighten on its own without touching everyday posting.
        yield return new PermissionDefinition(
            CommunityPermissions.PublishEmergency, Module, "Publish an Emergency alert.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityPermissions.ManagePost, Module, "Edit or delete your own post.")
        {
            MinimumTier = AccessTier.Verified,

            // A member must be able to take their own content down during a restriction. That is
            // what a data-protection request needs, and it is never the thing to withhold.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Comment, Module, "Join a discussion.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CommunityPermissions.ManageComment, Module, "Edit or delete your own comment.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityPermissions.React, Module, "React to content.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Share, Module, "Share content.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Bookmark, Module, "Save content.")
        {
            MinimumTier = AccessTier.Limited,

            // Saving is private and costs nobody anything. A new member gathering things to come
            // back to is exactly the behaviour onboarding is trying to produce.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Follow, Module, "Follow people and organizations.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Poll, Module, "Create and vote in polls.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Report, Module, "Report content.")
        {
            MinimumTier = AccessTier.Limited,

            // The one restriction that would make the platform less safe. Never withheld.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityPermissions.ModerationRead, Module, "Read the moderation queue.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Moderate, Module, "Decide moderation cases.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Pin, Module, "Pin a post or a comment.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityPermissions.Administer, Module, "Manage reactions and audience rules.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Admin", "SuperAdmin" }
        };
    }
}
