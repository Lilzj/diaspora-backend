using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Location.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// The kernel validates at startup that every permission named here exists, so a typo fails
/// the boot rather than silently denying a route forever.
/// </para>
/// </summary>
public sealed class LocationPolicyContributor : IPolicyContributor
{
    public string Module => LocationModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            LocationPolicies.ReadCatalog, Module, LocationPermissions.ReadCatalog);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.Search, Module, LocationPermissions.Search);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.Hyperlocal, Module, LocationPermissions.Hyperlocal);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.ReadProfile, Module, LocationPermissions.ReadProfile);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.ManageProfile, Module, LocationPermissions.ManageProfile);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.ReadExperience, Module, LocationPermissions.ReadExperience);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.ManageExperience, Module, LocationPermissions.ManageExperience);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.ReadPublic, Module, LocationPermissions.ReadPublic);

        yield return PolicyDefinition.ForPermission(
            LocationPolicies.ManageCatalog, Module, LocationPermissions.ManageCatalog);
    }
}
