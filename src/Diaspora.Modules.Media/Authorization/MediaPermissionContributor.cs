using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Media.Authorization;

/// <summary>
/// Declares the permissions the Media module owns.
/// <para>
/// The tier band is the interesting part, and it is not uniform. Uploading an image opens at
/// <see cref="AccessTier.Onboarding"/> because the reputation model scores
/// <c>profilePhotoAdded</c> as a trust signal — a member who cannot add a profile photograph
/// until they are verified cannot build the reputation that verifies them, which is a loop
/// with no entrance. Uploading a video opens at <see cref="AccessTier.Verified"/>, because
/// half a gigabyte of storage and a transcode is a real cost and an unverified account is
/// exactly who spends it maliciously.
/// </para>
/// <para>
/// Reading survives restriction. A suspended member still sees their own screens, and a screen
/// whose every image 403s is not a suspension — it looks like an outage.
/// </para>
/// </summary>
public sealed class MediaPermissionContributor : IPermissionContributor
{
    public string Module => MediaModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            MediaPermissions.Read, Module, "Read a file you are allowed to see.")
        {
            MinimumTier = AccessTier.Onboarding,

            // A restricted member's screens still render. Images that all fail look like a
            // broken product rather than a restricted account.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MediaPermissions.UploadImage, Module, "Upload a photograph.")
        {
            // Adding a profile photograph is part of finishing onboarding, and the reputation
            // model treats it as a trust signal. Gating it behind verification would make the
            // signal unreachable for exactly the members who need it.
            MinimumTier = AccessTier.Onboarding
        };

        yield return new PermissionDefinition(
            MediaPermissions.UploadDocument, Module, "Upload a document.")
        {
            // Verification evidence is a document, and a member submits it before they are
            // verified — by definition.
            MinimumTier = AccessTier.Onboarding
        };

        yield return new PermissionDefinition(
            MediaPermissions.UploadAudio, Module, "Upload a voice note.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            MediaPermissions.UploadVideo, Module, "Upload a video.")
        {
            // The most expensive thing a member can do. Half a gigabyte of storage plus a
            // transcode, and an unverified account is who spends it abusively.
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MediaPermissions.Manage, Module, "Change a file's description or visibility.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            MediaPermissions.Delete, Module, "Delete a file you own.")
        {
            MinimumTier = AccessTier.Limited,

            // A restricted member must be able to take their own content down. Removing that
            // ability during a suspension is the opposite of what a data-protection request
            // needs.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MediaPermissions.ManageCollections, Module, "Create and reorder galleries.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            MediaPermissions.Transform, Module, "Request a rendition of a file.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            MediaPermissions.SignedAccess, Module, "Mint a short-lived link to a private file.")
        {
            MinimumTier = AccessTier.Onboarding,

            // The verification wizard's reviewer flow and the member's own document preview
            // both need this before the member is verified.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MediaPermissions.Administer, Module, "Read any file, including quarantined ones.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            MediaPermissions.ManageProcessing, Module,
            "Requeue processing, rescan a file, release a quarantine.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Admin", "SuperAdmin" }
        };
    }
}
