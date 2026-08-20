using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Notifications.Authorization;

/// <summary>
/// Declares the permissions the Communication Platform owns.
/// <para>
/// <b>Every member-facing permission here is <c>GrantedWhenRestricted</c>, and that is the module's
/// central judgement.</b> Nowhere else on the platform is the whole band granted under suspension.
/// It is right here for one reason: a suspended, locked or closed account is precisely the account
/// that has been sent an explanation, an appeal route and a security warning — and a restriction
/// that silenced those would be the platform punishing somebody by refusing to tell them why.
/// </para>
/// <para>
/// The staff band is not. Authoring templates, retrying deliveries and reading analytics are
/// privileges of a role, and a restricted staff account should hold none of them.
/// </para>
/// <para>
/// The kernel constructs this before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class NotificationPermissionContributor : IPermissionContributor
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        foreach (var permission in MemberPermissions())
        {
            yield return permission;
        }

        foreach (var permission in StaffPermissions())
        {
            yield return permission;
        }
    }

    /// <summary>
    /// What a member may do with their own messages.
    /// <para>
    /// At <see cref="AccessTier.Onboarding"/> — the lowest tier the platform grants anything at — so
    /// that an account which has not finished onboarding can still read the message telling it how,
    /// and surviving restriction for the reason in the class summary. Not <c>Anonymous</c>: B2.0.1
    /// settled that nothing is granted there, because there is no authenticated subject to grant it
    /// to, and reading your own inbox needs an account.
    /// </para>
    /// </summary>
    private static IEnumerable<PermissionDefinition> MemberPermissions()
    {
        yield return new PermissionDefinition(
            NotificationPermissions.InboxRead, NotificationsModuleMetadata.Name,
            "Read your own notifications.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            NotificationPermissions.InboxManage, NotificationsModuleMetadata.Name,
            "Read, archive, pin, star and delete your own notifications.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            NotificationPermissions.PreferencesRead, NotificationsModuleMetadata.Name,
            "Read your own communication preferences.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            NotificationPermissions.PreferencesManage, NotificationsModuleMetadata.Name,
            "Change how you are contacted.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            NotificationPermissions.DevicesManage, NotificationsModuleMetadata.Name,
            "Register and remove devices for push notifications.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            NotificationPermissions.DigestsRead, NotificationsModuleMetadata.Name,
            "Read your own digests.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            NotificationPermissions.DigestsGenerate, NotificationsModuleMetadata.Name,
            "Generate a digest on demand.")
        {
            MinimumTier = AccessTier.Limited
        };
    }

    /// <summary>
    /// What speaking in the platform's own voice requires.
    /// <para>
    /// Publishing a template and managing the catalogue are administrator-only; authoring, reading
    /// delivery records and retrying are the wider staff band. The split follows the blast radius:
    /// a draft reaches nobody, a published template reaches everybody.
    /// </para>
    /// </summary>
    private static IEnumerable<PermissionDefinition> StaffPermissions()
    {
        yield return new PermissionDefinition(
            NotificationPermissions.TemplatesRead, NotificationsModuleMetadata.Name,
            "Read the notification template catalogue.")
        {
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            NotificationPermissions.TemplatesManage, NotificationsModuleMetadata.Name,
            "Author notification templates.")
        {
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            NotificationPermissions.TemplatesPublish, NotificationsModuleMetadata.Name,
            "Publish and retire notification templates.")
        {
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            NotificationPermissions.DeliveryRead, NotificationsModuleMetadata.Name,
            "Read delivery records. Never the message itself.")
        {
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            NotificationPermissions.DeliveryRetry, NotificationsModuleMetadata.Name,
            "Retry a failed delivery.")
        {
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            NotificationPermissions.AnalyticsRead, NotificationsModuleMetadata.Name,
            "Read notification analytics.")
        {
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            NotificationPermissions.CatalogManage, NotificationsModuleMetadata.Name,
            "Manage notification categories and routing rules.")
        {
            Roles = AdminRoles
        };
    }
}
