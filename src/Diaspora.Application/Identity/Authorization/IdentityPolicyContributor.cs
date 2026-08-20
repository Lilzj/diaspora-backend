using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// Declares Identity's authorisation policies to the kernel, which registers them with
/// ASP.NET Core.
/// <para>
/// B2 built these inline in <c>AddIdentityAuthentication</c>, which meant the host had to
/// know Identity's policy names. Now the host knows only that policies exist.
/// </para>
/// </summary>
public sealed class IdentityPolicyContributor : IPolicyContributor
{
    public string Module => IdentityModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        foreach (var (policy, permission) in DiasporaPolicies.PermissionPolicies)
        {
            yield return PolicyDefinition.ForPermission(policy, Module, permission);
        }

        foreach (var (policy, tier) in DiasporaPolicies.TierPolicies)
        {
            yield return PolicyDefinition.ForTier(policy, Module, tier);
        }
    }
}
