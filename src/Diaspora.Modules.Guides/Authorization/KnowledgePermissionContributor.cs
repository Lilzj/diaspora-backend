using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the permissions the Country Knowledge Directory owns.
/// <para>
/// <b>A second contributor from the same module, which the kernel accepts because contributors are
/// collected as a set rather than keyed by module.</b> Folding these into B8's contributor would
/// have worked identically and read worse: the band is a different thing — curation of what the
/// platform tells people, rather than authorship of it — and a reviewer auditing who can rearrange a
/// country page should be able to read that answer in one file.
/// </para>
/// <para>
/// <b>Every permission here is role-scoped and none is tier-scoped.</b> Curation is editorial
/// authority, and B2.0.1 is explicit that a tier measures progression while a role grants privilege.
/// A member who verifies their identity has proven who they are; they have not been given the page
/// every newcomer to a country sees. The tier floor is <c>Limited</c> on all of them because nothing
/// on this platform sits below it — a gate below Limited is granted to phone-unverified accounts —
/// and the role set is what actually decides.
/// </para>
/// <para>
/// The kernel constructs this before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class KnowledgePermissionContributor : IPermissionContributor
{
    /// <summary>
    /// Curation is an editorial job, so moderators hold it alongside administrators. A moderator
    /// already decides what stays up; deciding what goes first is the same kind of judgement.
    /// </summary>
    private static readonly HashSet<string> CuratorRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    /// <summary>
    /// The navigation catalogue and the set of countries served are structural rather than
    /// editorial: they are the shape of the whole product, and they belong to administrators.
    /// </summary>
    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            KnowledgePermissions.ManageCategories,
            Module,
            "Manage the knowledge navigation catalogue.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            KnowledgePermissions.ManageDirectory,
            Module,
            "Open, arrange, publish and archive a country's knowledge directory.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            KnowledgePermissions.Curate,
            Module,
            "Feature guides on a country shelf and set what to read next.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = CuratorRoles
        };

        yield return new PermissionDefinition(
            KnowledgePermissions.ManagePaths,
            Module,
            "Write and publish curated onboarding routes.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = CuratorRoles
        };

        yield return new PermissionDefinition(
            KnowledgePermissions.ViewAnalytics,
            Module,
            "Read country knowledge usage, the category breakdown and unanswered searches.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = CuratorRoles
        };
    }
}
