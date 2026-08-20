using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the permissions the Guides module owns.
/// <para>
/// One product judgement runs through the whole band: <b>writing is engagement, publishing is a
/// claim.</b> Drafting, voting, bookmarking and suggesting open at <see cref="AccessTier.Limited"/>
/// because they cost the community nothing and they are exactly the behaviour a new member should
/// be doing. Publishing, scheduling and asking Trust to stamp a guide open at
/// <see cref="AccessTier.Verified"/>, because a published guide is long-lived advice that carries
/// the platform's name to people who will act on it.
/// </para>
/// <para>
/// Staff capabilities — featuring, moderating, editing anyone's guide, administering the category
/// catalogue — are <b>role-scoped, not tier-scoped</b>, per B2.0.1: a tier measures progression, a
/// role grants privilege. Nothing here sits below <see cref="AccessTier.Limited"/>; a gate below it
/// is granted to phone-unverified accounts.
/// </para>
/// <para>
/// The kernel constructs this type before the container exists, so it has a public parameterless
/// constructor and no dependencies.
/// </para>
/// </summary>
public sealed class GuidePermissionContributor : IPermissionContributor
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        // Authoring — drafting is engagement, not a claim.
        yield return new PermissionDefinition(
            GuidePermissions.Create, Module, "Write a guide.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            GuidePermissions.EditOwn, Module, "Edit a guide you wrote.")
        {
            MinimumTier = AccessTier.Limited,

            // Correcting your own published advice is never the thing to withhold: wrong
            // information about a visa deadline hurts readers, not the restricted author.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            GuidePermissions.EditAny, Module, "Edit any guide.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            GuidePermissions.DeleteOwn, Module, "Delete a guide you wrote.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            GuidePermissions.ViewDrafts, Module, "Read your own unpublished drafts.")
        {
            MinimumTier = AccessTier.Limited,

            // Your own unpublished work stays readable to you under restriction.
            GrantedWhenRestricted = true
        };

        // Lifecycle — publishing is the progression gate.
        yield return new PermissionDefinition(
            GuidePermissions.Publish, Module, "Publish a guide.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            GuidePermissions.Schedule, Module, "Schedule a guide to publish later.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            GuidePermissions.ArchiveOwn, Module, "Archive a guide you wrote.")
        {
            MinimumTier = AccessTier.Limited,

            // Taking your own out-of-date guide down is a decision nobody should be able to block.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            GuidePermissions.RestoreOwn, Module, "Restore a guide you archived.")
        {
            MinimumTier = AccessTier.Limited
        };

        // Curation & verification.
        yield return new PermissionDefinition(
            GuidePermissions.Feature, Module, "Feature or unfeature any guide.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            GuidePermissions.SubmitVerification, Module, "Submit a guide for verification.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            GuidePermissions.Moderate, Module, "Hide, unhide and annotate any guide.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            GuidePermissions.ManageCategories, Module, "Manage the guide category catalogue.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        // Engagement — the cheapest, most valuable behaviour on the platform.
        yield return new PermissionDefinition(
            GuidePermissions.Vote, Module, "Mark a guide helpful or unhelpful.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            GuidePermissions.Bookmark, Module, "Save a guide to read later.")
        {
            MinimumTier = AccessTier.Limited,

            // Saving is private and costs nobody anything.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            GuidePermissions.Report, Module, "Report a guide.")
        {
            MinimumTier = AccessTier.Limited,

            // Reporting harmful advice must survive a restriction, or the restriction silences
            // the person best placed to flag it.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            GuidePermissions.Suggest, Module, "Propose an improvement to someone else's guide.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            GuidePermissions.ManageTags, Module, "Set the tags on a guide.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            GuidePermissions.ViewAnalytics, Module, "Read a guide's readership statistics.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}
