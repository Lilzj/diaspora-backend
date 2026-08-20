using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the policies the Knowledge Directory's endpoints reference.
/// <para>
/// Every policy names exactly one permission from <see cref="KnowledgePermissions"/>. The kernel
/// validates at startup that each named permission exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class KnowledgePolicyContributor : IPolicyContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            KnowledgePolicies.ManageCategories, Module, KnowledgePermissions.ManageCategories);

        yield return PolicyDefinition.ForPermission(
            KnowledgePolicies.ManageDirectory, Module, KnowledgePermissions.ManageDirectory);

        yield return PolicyDefinition.ForPermission(
            KnowledgePolicies.Curate, Module, KnowledgePermissions.Curate);

        yield return PolicyDefinition.ForPermission(
            KnowledgePolicies.ManagePaths, Module, KnowledgePermissions.ManagePaths);

        yield return PolicyDefinition.ForPermission(
            KnowledgePolicies.ViewAnalytics, Module, KnowledgePermissions.ViewAnalytics);
    }
}
