using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Financial.Authorization;

/// <summary>
/// Declares this module's ASP.NET authorization policies.
/// <para>
/// Three, each backed by a permission rather than by a role, because the platform authorises on
/// permissions and a policy naming a role would be a second vocabulary for the same decision.
/// </para>
/// </summary>
public sealed class FinancialPolicyContributor : IPolicyContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            FinancialAuthorizationPolicies.Member, Module, FinancialPermissions.WalletRead);

        yield return PolicyDefinition.ForPermission(
            FinancialAuthorizationPolicies.Operator, Module, FinancialPermissions.OperationsRun);

        yield return PolicyDefinition.ForPermission(
            FinancialAuthorizationPolicies.Analyst, Module, FinancialPermissions.ReportingRead);
    }
}
