using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Policies;

/// <summary>Every recommendation policy, enabled or not. Staff-only.</summary>
[Authorize(Permission = RecommendationPermissions.PoliciesRead)]
public sealed record GetRecommendationPoliciesQuery
    : IQuery<IReadOnlyList<RecommendationPolicyResponse>>;
