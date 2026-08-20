using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Infrastructure.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Policies;

/// <summary>
/// Reads the policy catalogue.
/// <para>
/// Returns the disabled ones too, which the enabled-only read the evaluator uses does not — an
/// administration screen that could not see what had been turned off would be an administration
/// screen nobody could use to turn it back on.
/// </para>
/// </summary>
public sealed class GetRecommendationPoliciesQueryHandler
    : IQueryHandler<GetRecommendationPoliciesQuery, IReadOnlyList<RecommendationPolicyResponse>>
{
    private readonly IRecommendationPolicyRepository _policies;
    private readonly RecommendationPolicyRuleRegistry _rules;

    public GetRecommendationPoliciesQueryHandler(
        IRecommendationPolicyRepository policies, RecommendationPolicyRuleRegistry rules)
    {
        _policies = policies;
        _rules = rules;
    }

    public async Task<Result<IReadOnlyList<RecommendationPolicyResponse>>> Handle(
        GetRecommendationPoliciesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var policies = await _policies.ListAllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RecommendationPolicyResponse>>(
            policies
                .Select(policy => RecommendationMapper.ToResponse(
                    policy, _rules.IsImplemented(policy.Key)))
                .ToList());
    }
}
