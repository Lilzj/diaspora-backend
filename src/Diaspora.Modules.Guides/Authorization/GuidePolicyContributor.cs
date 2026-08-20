using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// Every policy names exactly one permission from <see cref="GuidePermissions"/>. The kernel
/// validates at startup that each named permission exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class GuidePolicyContributor : IPolicyContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Create, Module, GuidePermissions.Create);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.EditOwn, Module, GuidePermissions.EditOwn);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.EditAny, Module, GuidePermissions.EditAny);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.DeleteOwn, Module, GuidePermissions.DeleteOwn);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.ViewDrafts, Module, GuidePermissions.ViewDrafts);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Publish, Module, GuidePermissions.Publish);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Schedule, Module, GuidePermissions.Schedule);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.ArchiveOwn, Module, GuidePermissions.ArchiveOwn);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.RestoreOwn, Module, GuidePermissions.RestoreOwn);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Feature, Module, GuidePermissions.Feature);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.SubmitVerification, Module, GuidePermissions.SubmitVerification);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Moderate, Module, GuidePermissions.Moderate);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.ManageCategories, Module, GuidePermissions.ManageCategories);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Vote, Module, GuidePermissions.Vote);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Bookmark, Module, GuidePermissions.Bookmark);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Report, Module, GuidePermissions.Report);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.Suggest, Module, GuidePermissions.Suggest);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.ManageTags, Module, GuidePermissions.ManageTags);

        yield return PolicyDefinition.ForPermission(
            GuidePolicies.ViewAnalytics, Module, GuidePermissions.ViewAnalytics);
    }
}
