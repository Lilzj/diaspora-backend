using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Recommendations.Authorization;

/// <summary>
/// The named ASP.NET policies this module's endpoints reference.
/// <para>
/// Three, not one per endpoint, and they are the three audiences: the member reading their own feed,
/// the curator tuning what everybody sees, and the analyst reading how it is going. A policy is the
/// endpoint-level gate that refuses before a body is read; the permission attribute on the command
/// refuses after the request is understood.
/// </para>
/// <para>
/// <b>Each carries its pre-B1.4.1 name as an alias.</b> The kernel registers both with ASP.NET Core
/// from this one definition, so the two names cannot come to mean different things. See
/// <see cref="RecommendationAuthorizationPolicies.Legacy"/>.
/// </para>
/// </summary>
public sealed class RecommendationPolicyContributor : IPolicyContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition
            .ForPermission(
                RecommendationAuthorizationPolicies.Member,
                Module,
                RecommendationPermissions.FeedRead)
            .WithAliases(RecommendationAuthorizationPolicies.Legacy.Member);

        yield return PolicyDefinition
            .ForPermission(
                RecommendationAuthorizationPolicies.Curator,
                Module,
                RecommendationPermissions.StrategiesManage)
            .WithAliases(RecommendationAuthorizationPolicies.Legacy.Curator);

        yield return PolicyDefinition
            .ForPermission(
                RecommendationAuthorizationPolicies.Analyst,
                Module,
                RecommendationPermissions.AnalyticsRead)
            .WithAliases(RecommendationAuthorizationPolicies.Legacy.Analyst);
    }
}
