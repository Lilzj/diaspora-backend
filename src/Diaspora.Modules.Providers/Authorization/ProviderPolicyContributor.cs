using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Providers.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// The kernel validates at startup that every permission named here exists, so a typo fails the
/// boot rather than silently denying a route forever.
/// </para>
/// </summary>
public sealed class ProviderPolicyContributor : IPolicyContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.Read, Module, ProviderPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.Register, Module, ProviderPermissions.Register);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.Manage, Module, ProviderPermissions.Manage);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.ManageStatus, Module, ProviderPermissions.ManageStatus);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.ManageMembers, Module, ProviderPermissions.ManageMembers);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.ManageServices, Module, ProviderPermissions.ManageServices);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.ManageAvailability, Module, ProviderPermissions.ManageAvailability);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.ManageMedia, Module, ProviderPermissions.ManageMedia);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.SubmitVerification, Module, ProviderPermissions.SubmitVerification);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.Review, Module, ProviderPermissions.Review);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.Recommend, Module, ProviderPermissions.Recommend);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.Bookmark, Module, ProviderPermissions.Bookmark);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.ReadStatistics, Module, ProviderPermissions.ReadStatistics);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.AdministerCatalog, Module, ProviderPermissions.AdministerCatalog);

        yield return PolicyDefinition.ForPermission(
            ProviderPolicies.Administer, Module, ProviderPermissions.Administer);
    }
}
