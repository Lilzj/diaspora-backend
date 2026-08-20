using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the policies the Country Intelligence Platform's endpoints reference.
/// <para>
/// Every policy names exactly one permission from <see cref="IntelligencePermissions"/>. The kernel
/// validates at startup that each named permission exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class IntelligencePolicyContributor : IPolicyContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.ManageNotices, Module, IntelligencePermissions.ManageNotices);

        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.VerifyNotices, Module, IntelligencePermissions.VerifyNotices);

        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.PublishNotices, Module, IntelligencePermissions.PublishNotices);

        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.ManageCatalogue, Module, IntelligencePermissions.ManageCatalogue);

        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.ManageAuthorities,
            Module,
            IntelligencePermissions.ManageAuthorities);

        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.ManageDirectory, Module, IntelligencePermissions.ManageDirectory);

        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.ManageVisas, Module, IntelligencePermissions.ManageVisas);

        yield return PolicyDefinition.ForPermission(
            IntelligencePolicies.ReadAnalytics, Module, IntelligencePermissions.ReadAnalytics);
    }
}
