using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Projects the Access Gateway's verdict onto the wire.
/// </summary>
public static class AccessDecisionMapper
{
    public static AccessDecisionResponse ToResponse(AccessDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new AccessDecisionResponse
        {
            AccessState = decision.AccessState.Name,
            AccountStatus = decision.AccountStatus.ToString(),
            Destination = decision.Destination.ToString(),
            Message = decision.Message,
            RequiredActions = decision.RequiredActions,
            GrantedFeatures = decision.Features.Granted,
            BlockedFeatures = decision.Features.Blocked.Select(b => b.Feature).ToArray(),
            Permissions = decision.Permissions,
            RestrictedUntil = decision.RestrictedUntil
        };
    }

    public static FeatureAccessResponse ToFeatureResponse(AccessDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var granted = decision.Features.Granted
            .Select(feature => new FeatureStateResponse
            {
                Name = feature,
                IsEnabled = true
            });

        var blocked = decision.Features.Blocked
            .Select(block => new FeatureStateResponse
            {
                Name = block.Feature,
                IsEnabled = false,
                BlockedReason = block.Reason,
                UnlockedAt = block.UnlockedAt
            });

        return new FeatureAccessResponse
        {
            AccessState = decision.AccessState.Name,
            Features = granted.Concat(blocked).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray()
        };
    }
}
