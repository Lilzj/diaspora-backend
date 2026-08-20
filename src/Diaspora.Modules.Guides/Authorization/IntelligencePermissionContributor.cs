using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the permissions the Country Intelligence Platform owns.
/// <para>
/// <b>A third contributor from the same module, which the kernel accepts because contributors are
/// collected as a set rather than keyed by module.</b> Folding these into B8's or B8.1's would work
/// identically and read worse: this band is authority over what the platform presents as government
/// information, and a reviewer auditing who can publish an immigration circular should be able to
/// read that answer in one file.
/// </para>
/// <para>
/// <b>Three role sets, and the narrowing is the point.</b> Editors write; verifiers confirm the
/// source; publishers make it visible. Moderators hold the first because it is editorial work of the
/// kind they already do. The other two are administrators only, because between them they decide what
/// this platform is willing to assert to a whole country under an official badge — and a workflow
/// where the person who transcribes also approves and publishes has no verification step at all.
/// </para>
/// <para>
/// <b>Every permission here is role-scoped and none is tier-scoped.</b> B2.0.1 is explicit that a
/// tier measures progression while a role grants privilege. The tier floor is <c>Limited</c> on all
/// of them because nothing on this platform sits below it — a gate below Limited is granted to
/// phone-unverified accounts — and the role set is what actually decides.
/// </para>
/// <para>
/// The kernel constructs this before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class IntelligencePermissionContributor : IPermissionContributor
{
    /// <summary>
    /// Transcribing a circular and arranging a directory are the same kind of editorial judgement, so
    /// moderators hold the writing permissions alongside administrators.
    /// </summary>
    private static readonly HashSet<string> EditorRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    /// <summary>
    /// Verifying a source, publishing to a country, and deciding which bodies count as official are
    /// each a statement the platform makes on its own authority. Administrators only.
    /// </summary>
    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            IntelligencePermissions.ManageNotices,
            Module,
            "Draft, edit, submit, archive and restore official notices.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = EditorRoles
        };

        yield return new PermissionDefinition(
            IntelligencePermissions.VerifyNotices,
            Module,
            "Confirm that an authority issued a submitted notice, or refuse it.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            IntelligencePermissions.PublishNotices,
            Module,
            "Publish a verified official notice, and supersede a live one.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            IntelligencePermissions.ManageCatalogue,
            Module,
            "Manage the notice category catalogue.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            IntelligencePermissions.ManageAuthorities,
            Module,
            "Record issuing authorities and raise verification claims about them.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            IntelligencePermissions.ManageDirectory,
            Module,
            "Maintain the embassy directory, emergency information and the holiday calendar.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = EditorRoles
        };

        yield return new PermissionDefinition(
            IntelligencePermissions.ManageVisas,
            Module,
            "Write and publish visa and immigration advisories.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = EditorRoles
        };

        yield return new PermissionDefinition(
            IntelligencePermissions.ReadAnalytics,
            Module,
            "Read country intelligence usage figures.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = EditorRoles
        };
    }
}
